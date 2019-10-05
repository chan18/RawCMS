﻿//******************************************************************************
// <copyright file="license.md" company="RawCMS project  (https://github.com/arduosoft/RawCMS)">
// Copyright (c) 2019 RawCMS project  (https://github.com/arduosoft/RawCMS)
// RawCMS project is released under GPL3 terms, see LICENSE file on repository root at  https://github.com/arduosoft/RawCMS .
// </copyright>
// <author>Daniele Fontani, Emanuele Bucarelli, Francesco Min�</author>
// <autogenerated>true</autogenerated>
//******************************************************************************
using McMaster.NETCore.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RawCMS.Library.Core.Extension;
using RawCMS.Library.Core.Helpers;
using RawCMS.Library.DataModel;
using RawCMS.Library.Schema;
using RawCMS.Library.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RawCMS.Library.Core
{
    public class AppEngine
    {
        private readonly string pluginFolder = null;
        private readonly IConfigurationRoot configuration;
        private readonly ILogger _logger;
        private readonly ReflectionManager reflectionManager;

        public List<Lambda> Lambdas { get; set; } = new List<Lambda>();

        public List<Plugin> Plugins { get; set; } = new List<Plugin>();

        public ReflectionManager ReflectionManager => reflectionManager;

        public AppEngine(ILogger _logger, Func<string, string> pluginPathLocator, ReflectionManager reflectionManager, IConfigurationRoot configuration)
        {
            this._logger = _logger;
            this.pluginFolder = pluginPathLocator.Invoke(AppContext.BaseDirectory);
            this.reflectionManager = reflectionManager;
            this.configuration = configuration;
        }

        public List<FieldTypeValidator> GetFieldTypeValidators()
        {
            return this.reflectionManager.GetAssignablesInstances<FieldTypeValidator>();
        }

        public void Init()
        {
        }

        private List<PluginLoader> loaders = new List<PluginLoader>();

        private void LoadPluginAssemblies()
        {
            _logger.LogInformation("LoadPluginAssemblies");
            loaders.Clear();

            List<Assembly> assembly = new List<Assembly>();
            assembly.Add(typeof(AppEngine).Assembly);

            List<Type> typesToAdd = new List<Type>();

            RecursiveGetAllTypes(ref assembly, ref typesToAdd);
            typesToAdd = typesToAdd.Distinct().ToList();

            _logger.LogInformation($"ASSEMBLY LOAD COMPLETED");

            // create plugin loaders
            var pluginsDir = pluginFolder ?? Path.Combine(AppContext.BaseDirectory, "plugins");

            _logger.LogInformation($"Loading plugin using {pluginsDir}");

            var pluginFiles = Directory.GetFiles(pluginsDir, "plugin.config", SearchOption.AllDirectories);

            _logger.LogDebug($"Found  {string.Join(",", pluginFiles)}");


            List<Assembly> contracts = new List<Assembly>();

            foreach (var pluginInfo in pluginFiles)
            {
                if (pluginInfo.Contains("Contracts") || pluginInfo.Contains("Extension"))
                {
                    var loader = PluginLoader.CreateFromConfigFile(
                   filePath: pluginInfo,
                   sharedTypes: typesToAdd.ToArray());


                    var contract = loader.LoadDefaultAssembly();
                    contracts.Add(contract);
                }
            }
            RecursiveGetAllTypes(ref contracts, ref typesToAdd);

            typesToAdd = typesToAdd.Distinct().ToList();

            var checkextraload=typesToAdd.Where(x => x.Assembly.FullName.Contains("Contracts")).ToList();
            var checkextraload2 = typesToAdd.Where(x => x.Assembly.FullName.Contains("Extension")).ToList();

            foreach (var pluginInfo in pluginFiles)
            {
                _logger.LogInformation($"Loading plugin  {pluginInfo}");
                var loader = PluginLoader.CreateFromConfigFile(
                filePath: pluginInfo,
                sharedTypes: typesToAdd.ToArray());
                loaders.Add(loader);
            }


        }

        private List<Type> RecursiveGetAllTypes(ref List<Assembly> assembly, ref List<Type> typesToAdd)
        {
            RecoursiveAddAssembly(typeof(AppEngine).Assembly, assembly);
            assembly = assembly.Distinct().ToList();

            
            foreach (var ass in assembly)
            {
                _logger.LogDebug($"scanning {ass.FullName}..");
                Type[] types = ass.GetTypes();
                foreach (var type in types)
                {
                    if (type.IsPublic)
                    {
                        _logger.LogDebug($"Added {type.FullName}..");
                        typesToAdd.Add(type);
                    }
                }

                typesToAdd.AddRange(types);
            }

            return typesToAdd;
        }

        public static AppEngine Create(string pluginPath, ILogger logger, ReflectionManager reflectionManager, IServiceCollection services, IConfigurationRoot configuration)
        {
            logger.LogInformation("CREATING RAWCMS APPENGINE");
            var appEngine = new AppEngine(
                  logger,
                  basedir =>
                  {
                      var folder = basedir + pluginPath;
                      if (Path.IsPathRooted(pluginPath))
                      {
                          folder = pluginPath;
                      }

                      return Path.GetFullPath(folder);//Directory.GetDirectories(folder).FirstOrDefault();
                  },
                  reflectionManager,
                  configuration
              );//Hardcoded for dev

            appEngine.Init();

            services.AddSingleton<ILogger>(x => logger);
            services.AddSingleton<AppEngine>(x => appEngine);
            appEngine.LoadPlugins(services);
            return appEngine;
        }

        private void RecoursiveAddAssembly(Assembly assembly, List<Assembly> assemblyList)
        {
            foreach (AssemblyName assName in assembly.GetReferencedAssemblies())
            {
                if (!assemblyList.Any(x => x.FullName == assName.FullName))
                {
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace($"Adding {assName.FullName}");
                    }

                    Assembly.Load(assName);
                    var ass = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName(false).Name == assName.Name);
                    assemblyList.Add(ass);
                    RecoursiveAddAssembly(ass, assemblyList);
                }
            }
        }

        private void LoadPlugins(IServiceCollection services)
        {
            _logger.LogDebug("Load plugins");
            LoadPluginAssemblies();

            var pluginTypes = GetPluginsTypes();//GetAnnotatedInstances<Plugin>();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                Plugins.ForEach(x =>
                {
                    _logger.LogDebug("Plugin found {0}", x.Name);
                });
            }

            foreach (var pluginType in pluginTypes)
            {
                services.AddSingleton(pluginType);
            }

            LoadPluginSettings(pluginTypes, configuration, services);

            var sp = services.BuildServiceProvider();

            _logger.LogDebug("Create plugin instances");
            foreach (var pluginType in pluginTypes)
            {
                _logger.LogDebug($" Create plugin instance  {pluginType.Name}");
                var plugin = sp.GetService(pluginType) as Plugin;
                Plugins.Add(plugin);
            }

            //Core plugin must be the first to be called. This ensure it also in case thirdy party define malicius priority.
            int minPriority = 0;
            Plugins.ForEach(x => { if (x.Priority <= minPriority) { minPriority = x.Priority - 1; } });
            Plugin corePlugin = Plugins.Single(x => x.Name == "Core");
            corePlugin.Priority = minPriority;
        }

        private void LoadPluginSettings(List<Type> pluginTypes, IConfigurationRoot configuration, IServiceCollection services)
        {
            _logger.LogDebug($"LoadPluginSettings");

            MongoSettings instance = MongoSettings.GetMongoSettings(configuration);
            var tmpService = new CRUDService(new MongoService(instance, _logger), instance, this);

            foreach (var plugin in pluginTypes)
            {
                _logger.LogDebug($"checking {plugin.FullName}");
                Type confitf = plugin.GetInterface("IConfigurablePlugin`1");//TODO: remove hardcoded reference to generic
                if (confitf != null)
                {
                    _logger.LogDebug($" {plugin.FullName} need a configuration");
                    Type confType = confitf.GetGenericArguments()[0];

                    ItemList confItem = tmpService.Query("_configuration", new DataQuery()
                    {
                        PageNumber = 1,
                        PageSize = 1,
                        RawQuery = @"{""plugin_name"":""" + plugin.FullName + @"""}"
                    });

                    JObject confToSave = null;

                    if (confItem.TotalCount == 0)
                    {
                        _logger.LogDebug($" {plugin.FullName} no persisted configuration found. Using default");
                        confToSave = new JObject
                        {
                            ["plugin_name"] = plugin.FullName,
                            ["data"] = JToken.FromObject(Activator.CreateInstance(confType))
                        };
                        tmpService.Insert("_configuration", confToSave);
                        _logger.LogDebug($" {plugin.FullName} default config saved to database");
                    }
                    else
                    {
                        confToSave = confItem.Items.First as JObject;
                        _logger.LogDebug($" {plugin.FullName} configuration found");
                    }

                    object objData = confToSave["data"].ToObject(confType);

                    _logger.LogDebug($" {plugin.FullName} configuration added to container");
                    services.AddSingleton(confType, objData);
                }
            }
        }

        private List<Type> GetPluginsTypes()
        {
            _logger.LogInformation($" Getting all plugin types");
            List<Type> plugins = new List<Type>();
            // Create an instance of plugin types
            plugins.AddRange(GetPluginTypes<Plugin>());
            return plugins;
        }

        private void LoadLambdas(IServiceProvider provider)
        {
            _logger.LogDebug("Discover Lambdas in Bundle");

            List<Type> lambdas = this.reflectionManager.GetImplementors<Lambda>();
            lambdas.AddRange(GetPluginTypes<Lambda>());

            foreach (var lambda in lambdas)
            {
                try
                {
                    _logger.LogDebug($"loading Lambdas {lambda} ");
                    var lambdaInstance = provider.GetService(lambda) as Lambda;
                    if (lambdaInstance != null)
                    {
                        _logger.LogDebug($"loading Lambdas {lambdaInstance.Name} - {lambdaInstance.Description} - {lambdaInstance.GetType().FullName} ");
                        this.Lambdas.Add(lambdaInstance);
                    }
                    else
                    {
                        _logger.LogDebug($"error during lambda init {lambda} ");
                    }
                }
                catch (Exception err)
                {
                    _logger.LogWarning($"error during lambda, lambda skipped {lambda} {err.Message}");
                    _logger.LogError(err, "");
                }
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                DumpLambdaInfo();
            }
        }

        private List<Type> GetPluginTypes<T>()
        {
            var result = new List<Type>();
            foreach (var loader in loaders)
            {
                foreach (var type in loader
                    .LoadDefaultAssembly()
                    .GetTypes()
                    .Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    result.Add(type);
                }
            }

            return result;
        }

        private void DumpLambdaInfo()
        {
            var types = this.Lambdas.Select(x => x.GetType().BaseType).Distinct().ToList();

            foreach (var type in types)
            {
                _logger.LogDebug("");
                _logger.LogDebug($"For type {type.FullName}");

                this.Lambdas.Where(x => x.GetType().BaseType == type).ToList().ForEach(x =>
                {
                    _logger.LogDebug($">> {x.Name} - {x.Description} -  {x.GetType().FullName}");
                });
            }
        }

        public void InvokeConfigure(IApplicationBuilder app)
        {
            _logger.LogDebug($"invoking configuraton");
            

            this.Plugins.OrderBy(x => x.Priority).ToList().ForEach(x =>
            {
                _logger.LogDebug($" > invoking configuraton on plugin {x.Name}");
                x.Configure(app);
            });
        }

        public void InvokeConfigureServices(List<Assembly> ass, IMvcBuilder builder, IServiceCollection services, IConfigurationRoot configuration)
        {
            _logger.LogDebug($"invoking InvokeConfigureServices");

            this.Plugins.OrderBy(x => x.Priority).ToList().ForEach(x =>
            {
                _logger.LogDebug($" > invoking configuraton on plugin {x.Name}");
                x.Setup(configuration);
                x.ConfigureMvc(builder);
                ass.Add(x.GetType().Assembly);
            });

            this.DiscoverLambdasInBundle(services);

            var temp =services.BuildServiceProvider();
            this.LoadLambdas(temp);

        }

        public void InvokePostConfigureServices(IServiceCollection services)
        {
            _logger.LogDebug($"invoking InvokePostConfigureServices");

            var activationMap = new Dictionary<Type, Type>();
            this.Plugins.OrderBy(x => x.Priority).ToList().ForEach(x =>
            {
                _logger.LogDebug($" > invoking configuraton on plugin {x.Name}");
                x.ConfigureServices(services);

                var delta = x.GetActivationMap();
                foreach (var key in delta.Keys)
                {
                    var replacement = activationMap.FirstOrDefault(y => y.Key.FullName == key.FullName);
                    if (replacement.Key!=null)
                    {
                        activationMap[replacement.Key] = delta[key];
                    }
                    else
                    {
                        activationMap[key] = delta[key];
                    }
                }
            });

            //foreach (var activation in activationMap)
            //{                
            //    this.reflectionManager.InvokeGenericMethod(null,
            //        typeof(ServiceCollectionServiceExtensions), 
            //        "AddSingleton", 
            //        new Type[] { activation.Key, activation.Value }, 
            //        new object[] { services });
            //}

            
        }

        /// <summary>
        /// Find and load all lambas already loaded with main bundle (no dinamycs)
        /// </summary>
        private void DiscoverLambdasInBundle(IServiceCollection services)
        {
            _logger.LogDebug("Discover Lambdas in Bundle");

            List<Type> lambdas = this.reflectionManager.GetImplementors<Lambda>();

            lambdas.AddRange(GetPluginTypes<Lambda>());

            foreach (Type type in lambdas)
            {
                _logger.LogDebug($"Lambda found {type.FullName}");
                services.AddSingleton(type);
            }
        }


        public IServiceCollection AddSingletonWithOverride<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
        {
            return services.AddSingletonWithOverride<TService, TImplementation>(this);
        }
    }
}