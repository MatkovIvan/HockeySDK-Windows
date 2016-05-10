﻿namespace Microsoft.HockeyApp
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Channel;
    using Extensibility;
    using Extensibility.Implementation;
    using Extensibility.Implementation.Tracing;
    using Extensibility.Windows;
    using Services;
    /// <summary>
    /// Windows app Initializer is TelemetryConfiguration and TelemetryModules 
    /// Bootstrap the WindowsApps SDK.
    /// </summary>
    internal static class WindowsAppInitializer
    {
        private static IUnhandledExceptionTelemetryModule unhandledExceptionModule = null;

        /// <summary>
        /// Initializes default configuration and starts automatic telemetry collection for specified WindowsCollectors flags. Must specify InstrumentationKey as a parameter or in configuration file.
        /// <param name="instrumentationKey">Telemetry configuration.</param>
        /// <param name="configuration">Telemetry configuration.</param>
        /// </summary>
        public static Task InitializeAsync(string instrumentationKey, TelemetryConfiguration configuration)
        {
            Guid instrumentationKeyGuid;
            if (!Guid.TryParse(instrumentationKey, out instrumentationKeyGuid))
            {
                throw new ArgumentException(string.Format(System.Globalization.CultureInfo.InvariantCulture, "instrumentationKey {0} is incorrect. It must be a string representation of a GUID", instrumentationKey));
            }

            // breeze accepts instrumentation key in 32 digits separated by hyphens format only.
            instrumentationKey = instrumentationKeyGuid.ToString("D");
            return Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(t => Initalize(instrumentationKey, configuration));
        }

        private static void Initalize(string instrumentationKey, TelemetryConfiguration configuration)
        {
            if (configuration == null)
            {
                configuration = new TelemetryConfiguration();
            }

            if (configuration.EnableDiagnostics)
            {
                CoreEventSource.Log.Enabled = true;
            }

            configuration.InstrumentationKey = instrumentationKey;
            if (configuration.Collectors.HasFlag(WindowsCollectors.Metadata))
            {
                configuration.ContextInitializers.Add(new DeviceContextInitializer());
                configuration.ContextInitializers.Add(new ComponentContextInitializer());
                configuration.TelemetryInitializers.Add(new UserContextInitializer());
            }

            configuration.TelemetryChannel = new PersistenceChannel();
            if (!string.IsNullOrEmpty(configuration.EndpointAddress)) 
            {
                configuration.TelemetryChannel.EndpointAddress = configuration.EndpointAddress;
            }

            TelemetryConfigurationFactory.Instance.Initialize(configuration);
            TelemetryConfiguration.Active = configuration;

            if (configuration.Collectors.HasFlag(WindowsCollectors.Session))
            {
                SessionTelemetryModule sessionModule = new SessionTelemetryModule();
                sessionModule.Initialize(configuration);
                TelemetryModules.Instance.Modules.Add(sessionModule);
            }

            if (configuration.Collectors.HasFlag(WindowsCollectors.UnhandledException))
            {
                LazyInitializer.EnsureInitialized(ref unhandledExceptionModule, 
                    () => {
                        var module = ServiceLocator.GetService<IUnhandledExceptionTelemetryModule>();
                        module.Initialize(configuration);
                        return module;
                    });

                TelemetryModules.Instance.Modules.Add(unhandledExceptionModule);
            }

            ((HockeyClient)HockeyClient.Current).Initialize();
        }
    }
}
