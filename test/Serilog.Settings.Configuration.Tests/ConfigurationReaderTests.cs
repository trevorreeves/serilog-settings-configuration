using System.Collections.Generic;
using Serilog.Settings.Configuration;
using Serilog.Formatting;
using Serilog.Formatting.Json;
using Xunit;
using System.Reflection;
using System.Linq;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Configuration;

namespace Serilog.Settings.Configuration.Tests
{
    public class ConfigurationReaderTests
    {
        [Fact]
        public void ConvertToType_StringValuesConvertToDefaultInstancesIfTargetIsInterface()
        {
            var result = ConfigurationReader.ConvertToType("Serilog.Formatting.Json.JsonFormatter, Serilog", typeof(ITextFormatter));
            Assert.IsType<JsonFormatter>(result);
        }

        [Fact]
        public void SelectConfigurationMethod_CallableMethodsAreSelected()
        {
            var options = typeof(DummyLoggerConfigurationExtensions).GetTypeInfo().DeclaredMethods.ToList();
            Assert.Equal(2, options.Count(mi => mi.Name == "DummyRollingFile"));
            var suppliedArguments = new Dictionary<string, string>
            {
                {"pathFormat", "C:\\" }
            };

            var selected = ConfigurationReader.SelectConfigurationMethod(options, "DummyRollingFile", suppliedArguments);
            Assert.Equal(typeof(string), selected.GetParameters()[1].ParameterType);
        }

        [Fact]
        public void SelectConfigurationMethod_MethodsAreSelectedBasedOnCountOfMatchedArguments()
        {
            var options = typeof(DummyLoggerConfigurationExtensions).GetTypeInfo().DeclaredMethods.ToList();
            Assert.Equal(2, options.Count(mi => mi.Name == "DummyRollingFile"));
            var suppliedArguments = new Dictionary<string, string>()
            {
                { "pathFormat", "C:\\" },
                { "formatter", "SomeFormatter, SomeAssembly" }
            };

            var selected = ConfigurationReader.SelectConfigurationMethod(options, "DummyRollingFile", suppliedArguments);
            Assert.Equal(typeof(ITextFormatter), selected.GetParameters()[1].ParameterType);
        }

        [Fact]
        public void GetMethodCalls__MultipleMethodsOfTheSameNameAreParsed()
        {
            var optionVals = new Dictionary<string, string>()
                {
                    {"Serilog:WriteTo:1:Name", "LiterateConsole"},
                    {"Serilog:WriteTo:1:Args:restrictedToMinimumLevel", "Information"},

                    {"Serilog:WriteTo:2:Name", "LiterateConsole"},
                    {"Serilog:WriteTo:2:Args:restrictedToMinimumLevel", "Error"}
                };
            var inMemoryConnectionOptions = new MemoryConfigurationSource { InitialData = optionVals };
            var optionsConfig = new ConfigurationBuilder().Add(inMemoryConnectionOptions).Build();

            var methodInfo = ConfigurationReader.GetMethodCalls(optionsConfig.GetSection("Serilog:WriteTo"));

            Assert.Equal(2, methodInfo.Count());
        }
    }
}
