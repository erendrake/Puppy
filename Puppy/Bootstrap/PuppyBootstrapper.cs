﻿#region Usings

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Reflection;
using System.Windows;
using Microsoft.Practices.Prism.Logging;
using Microsoft.Practices.Prism.MefExtensions;
using PuppyFramework.Helpers;
using PuppyFramework.Interfaces;
using PuppyFramework.Models;
using PuppyFramework.Properties;
using PuppyFramework.Services;
using PuppyFramework.UI;
using PuppyFramework.ViewModels;

#endregion

namespace PuppyFramework.Bootstrap
{
    public class PuppyBootstrapper : MefBootstrapper
    {
        #region Fields

        private readonly string _assemblyName;
        private readonly string _version;
        protected SerilogLogger _logger;

        #endregion

        #region Properties

        public BootstrapConfig BootstrapConfig { get; private set; }

        #endregion

        #region Constructors

        public PuppyBootstrapper()
        {
            var assembly = Assembly.GetAssembly(typeof (PuppyBootstrapper)).GetName();
            _assemblyName = assembly.Name;
            _version = assembly.Version.ToString();
        }

        #endregion

        #region Methods

        protected override void ConfigureAggregateCatalog()
        {
            AggregateCatalog.Catalogs.Add(new AssemblyCatalog(typeof (PuppyBootstrapper).Assembly));
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                AggregateCatalog.Catalogs.Add(new AssemblyCatalog(entryAssembly));
            }
        }

        protected override void ConfigureContainer()
        {
            Container.ComposeExportedValue<ILogger>(_logger);
            RegisterDefaultServicesIfMissing();
            base.ConfigureContainer();
        }

        protected override ILoggerFacade CreateLogger()
        {
            var source = string.Format("{0} {1}", _assemblyName, _version);
            _logger = _logger ?? new SerilogLogger(source);
            return _logger;
        }

        protected override DependencyObject CreateShell()
        {
            var customShell = Container.GetExportedValueOrDefault<IShell>();
            BootstrapConfig.IsUsingCustomShell = customShell != null;
            var shell = (customShell ?? Container.GetExportedValue<DefaultShell>()) as Window;
            if (shell == null)
            {
                throw new InvalidCastException(Resources._invalidShellTypeException);
            }
            _logger.Log("Created shell {ClassName:l}", Category.Info, null, shell.GetType().FullName);
            return shell;
        }

        private T GetAppSetting<T>(string key, T defaultValue = default(T))
        {
            try
            {
                _logger.Log("Reading settings from App.Config", Category.Info);
                var value = ConfigurationManager.AppSettings[key];
                _logger.Log("Found value {Value} for key {Key}", Category.Info, null, value, key);
                return TConverter.ChangeType<T>(value);
            }
            catch (Exception)
            {
                _logger.Log("Error fetching app setting for key {Key}; using default value", Category.Warn, null, key);
                return defaultValue;
            }
        }

        protected override void InitializeShell()
        {
            base.InitializeShell();
            var shell = (IShell) Shell;
            var customShellViewModel = Container.GetExportedValueOrDefault<IShellViewModel>();
            shell.ViewModel = customShellViewModel
                              ?? Container.GetExportedValue<DefaultShellViewModel>();

            BootstrapConfig.IsUsingCustomShellViewModel = customShellViewModel != null;

            RunBootableServices();

            _logger.Log("Initialized shell {ClassName:l}", Category.Info, null, shell.GetType().FullName);
            shell.Show();
            _logger.Log("Showing shell {ClassName:l}", Category.Info, null, shell.GetType().FullName);
        }

        private BootstrapConfig ReadConfigFromApplicationSettings()
        {
            var enableMenu = GetAppSetting(MagicStrings.Keys.ASK_ADD_MAIN_MENU, true);
            var enableUpdater = GetAppSetting(MagicStrings.Keys.ASK_ENABLE_UPDATER_SERVICE, false);
            _logger.Log("Created BootstrapConfig from App.Config", Category.Info);
            return new BootstrapConfig
            {
                AddMainMenu = enableMenu,
                EnableUpdaterService = enableUpdater,
            };
        }

        private void RegisterDefaultServicesIfMissing()
        {
            var menuFactory = Container.GetExportedValueOrDefault<IMenuFactory>() ?? new DefaultMenuFactory();
            Container.ComposeExportedValue(menuFactory);

            var menuComparer = Container.GetExportedValueOrDefault<IComparer<MenuItemBase>>() ?? new WeightBasedMenuItemComparer();
            Container.ComposeExportedValue(menuComparer);

            var menuRegisterService = Container.GetExportedValueOrDefault<IMenuRegisterService>() ?? new MenuRegisterService(menuComparer);
            Container.ComposeExportedValue(menuRegisterService);
        }

        public void Run(BootstrapConfig config = null)
        {
            CreateLogger();
            if (config == null)
            {
                config = ReadConfigFromApplicationSettings();
            }
            BootstrapConfig = config;
            _logger.Log("Running app", Category.Info);
            _logger.Log("Add MainMenu? {Status:l}", Category.Info, null, BootstrapConfig.AddMainMenu);
            _logger.Log("Enable UpdaterService? {Status:l}", Category.Info, null, BootstrapConfig.EnableUpdaterService);
            _logger.Log("Register Default Prism Library Services? {Status:l}", Category.Info, null, BootstrapConfig.RegisterDefaultPrismServices);
            base.Run(config.RegisterDefaultPrismServices);
        }

        public override void Run(bool registerDefaultPrismServices)
        {
            throw new InvalidOperationException(Resources._invalidRunOperationException);
        }

        private void RunBootableServices()
        {
            var services = Container.GetExportedValues<IBootableService>();
            foreach (var service in services)
            {
                service.Boot(BootstrapConfig);
            }
        }

        #endregion
    }
}