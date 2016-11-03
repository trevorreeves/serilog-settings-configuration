using System.Collections.Generic;
using Serilog.Formatting;
using Serilog.Formatting.Json;
using Xunit;
using System.Reflection;
using System.Linq;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Configuration;
using System;

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
        public void GetMethodCalls_MultipleMethodsOfTheSameNameAreParsed()
        {
            var config = LoadConfig(new Dictionary<string, string>
                {
                    {"Serilog:WriteTo:1:Name", "LiterateConsole"},
                    {"Serilog:WriteTo:1:Args:restrictedToMinimumLevel", "Information"},

                    {"Serilog:WriteTo:2:Name", "LiterateConsole"},
                    {"Serilog:WriteTo:2:Args:restrictedToMinimumLevel", "Error"}
                });

            var methodInfo = ConfigurationReader.GetMethodCalls(config.GetSection("Serilog:WriteTo"));

            Assert.Equal(2, methodInfo.Count());
            Assert.Equal("LiterateConsole", methodInfo[0].Name);
            Assert.Equal(1, methodInfo[0].Arguments.Count);
            Assert.Equal("Information", methodInfo[0].Arguments["restrictedToMinimumLevel"]);

            Assert.Equal("LiterateConsole", methodInfo[1].Name);
            Assert.Equal(1, methodInfo[1].Arguments.Count);
            Assert.Equal("Error", methodInfo[1].Arguments["restrictedToMinimumLevel"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void GetMethodCalls_MethodWithNoNameThrows(string name)
        {
            var config = LoadConfig(new Dictionary<string, string>
                {
                    {"Serilog:WriteTo:1:Name", name},
                    {"Serilog:WriteTo:1:Args:restrictedToMinimumLevel", "Information"}
                });

            Assert.Throws<InvalidOperationException>(() => ConfigurationReader.GetMethodCalls(config.GetSection("Serilog:WriteTo")));
        }

        [Fact]
        public void GetMethodCalls_MethodWithNoArgumentsParsed()
        {
            var config = LoadConfig(new Dictionary<string, string>
                {
                    {"Serilog:WriteTo:1:Name", "LiterateConsole"}
                });

            var methodInfo = ConfigurationReader.GetMethodCalls(config.GetSection("Serilog:WriteTo"));

            Assert.Equal(1, methodInfo.Count());
            Assert.Equal("LiterateConsole", methodInfo[0].Name);
            Assert.Equal(0, methodInfo[0].Arguments.Count);
        }

        [Fact]
        public void GetMethodCalls_ArgumentWithEnvironmentVariableGetsExpanded()
        {
            var expected = Environment.GetEnvironmentVariable("TEMP");

            var config = LoadConfig(new Dictionary<string, string>
                {
                    {"Serilog:WriteTo:1:Name", "EnvironmentVariableTest"},
                    {"Serilog:WriteTo:1:Args:tempValue", "test-%TEMP%-test"}
                });

            var methodInfo = ConfigurationReader.GetMethodCalls(config.GetSection("Serilog:WriteTo"));

            Assert.Equal(1, methodInfo.Count());
            Assert.Equal("EnvironmentVariableTest", methodInfo[0].Name);
            Assert.Equal(1, methodInfo[0].Arguments.Count);
            Assert.Equal($"test-{expected}-test", methodInfo[0].Arguments["tempValue"]);
        }

        private IConfigurationRoot LoadConfig(Dictionary<string, string> configValues)
        {
            var inMemoryConfigSource = new MemoryConfigurationSource { InitialData = configValues };
            return new ConfigurationBuilder().Add(inMemoryConfigSource).Build();
        }
    }
}
