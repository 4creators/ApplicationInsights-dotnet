﻿namespace Microsoft.ApplicationInsights.DataContracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AI;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.External;
    using Microsoft.ApplicationInsights.TestFramework;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;
    
    [TestClass]
    public class DependencyTelemetryTest
    {
        [TestMethod]
        public void RemoteDependencyTelemetrySerializesToJson()
        {
            DependencyTelemetry expected = this.CreateRemoteDependencyTelemetry();
            var item = TelemetryItemTestHelper.SerializeDeserializeTelemetryItem<DependencyTelemetry, AI.RemoteDependencyData>(expected);

            Assert.Equal(expected.Timestamp, new DateTimeOffset(Convert.ToDateTime(item.time)));
            Assert.Equal(expected.Sequence, item.seq);
            Assert.Equal(expected.Context.InstrumentationKey, item.iKey);
            Assert.Equal(expected.Context.Tags.ToArray(), item.tags.ToArray());
            Assert.Equal(typeof(AI.RemoteDependencyData).Name, item.data.baseType);

            Assert.Equal(expected.Id, item.data.baseData.id);
            Assert.Equal(expected.ResultCode, item.data.baseData.resultCode);
            Assert.Equal(expected.Name, item.data.baseData.name);
            Assert.Equal(expected.Duration, TimeSpan.Parse(item.data.baseData.duration));
            Assert.Equal(expected.DependencyTypeName, item.data.baseData.dependencyTypeName);

            Assert.Equal(expected.Success, item.data.baseData.success);
            Assert.Equal(expected.Properties.ToArray(), item.data.baseData.properties.ToArray());
        }

        [TestMethod]
        public void SerializeWritesNullValuesAsExpectedByEndpoint()
        {
            DependencyTelemetry original = new DependencyTelemetry();
            original.Name = null;
            original.Data = null;
            original.DependencyTypeName = null;
            original.Success = null;
            ((ITelemetry)original).Sanitize();
            var item = TelemetryItemTestHelper.SerializeDeserializeTelemetryItem<DependencyTelemetry, AI.RemoteDependencyData>(original);

            Assert.Equal(2, item.data.baseData.ver);
        }

        [TestMethod]
        public void RemoteDependencyTelemetrySerializesStructuredIKeyToJsonCorrectlyPreservingPrefixCasing()
        {
            DependencyTelemetry expected = this.CreateRemoteDependencyTelemetry();
            expected.Context.InstrumentationKey = "AIC-" + expected.Context.InstrumentationKey;
            var item = TelemetryItemTestHelper.SerializeDeserializeTelemetryItem<DependencyTelemetry, AI.RemoteDependencyData>(expected);

            Assert.Equal(expected.Context.InstrumentationKey, item.iKey);
        }

        [TestMethod]
        public void RemoteDependencyTelemetrySerializeCommandNameToJson()
        {
            DependencyTelemetry expected = this.CreateRemoteDependencyTelemetry("Select * from Customers where CustomerID=@1");
            var item = TelemetryItemTestHelper.SerializeDeserializeTelemetryItem<DependencyTelemetry, AI.RemoteDependencyData>(expected);
            AI.RemoteDependencyData dp = item.data.baseData;
            Assert.Equal(expected.Data, dp.data);
        }

        [TestMethod]
        public void RemoteDependencyTelemetrySerializeNullCommandNameToJson()
        {
            DependencyTelemetry expected = this.CreateRemoteDependencyTelemetry(null);
            var item = TelemetryItemTestHelper.SerializeDeserializeTelemetryItem<DependencyTelemetry, AI.RemoteDependencyData>(expected);
            AI.RemoteDependencyData dp = item.data.baseData;
            Assert.Null(dp.data);
        }
        
        [TestMethod]
        public void RemoteDependencyTelemetrySerializeEmptyCommandNameToJson()
        {
            DependencyTelemetry expected = this.CreateRemoteDependencyTelemetry(string.Empty);
            var item = TelemetryItemTestHelper.SerializeDeserializeTelemetryItem<DependencyTelemetry, AI.RemoteDependencyData>(expected);
            AI.RemoteDependencyData dp = item.data.baseData;
            Assert.Null(dp.data);
        }

        [TestMethod]
        public void SanitizeWillTrimAppropriateFields()
        {
            DependencyTelemetry telemetry = new DependencyTelemetry();
            telemetry.Name = new string('Z', Property.MaxNameLength + 1);
            telemetry.Data = new string('Y', Property.MaxCommandNameLength + 1);
            telemetry.DependencyTypeName = new string('D', Property.MaxDependencyTypeLength + 1);
            telemetry.Properties.Add(new string('X', Property.MaxDictionaryNameLength) + 'X', new string('X', Property.MaxValueLength + 1));
            telemetry.Properties.Add(new string('X', Property.MaxDictionaryNameLength) + 'Y', new string('X', Property.MaxValueLength + 1));
            
            ((ITelemetry)telemetry).Sanitize();

            Assert.Equal(new string('Z', Property.MaxNameLength), telemetry.Name);
            Assert.Equal(new string('Y', Property.MaxCommandNameLength), telemetry.Data);
            Assert.Equal(new string('D', Property.MaxDependencyTypeLength), telemetry.DependencyTypeName);

            Assert.Equal(2, telemetry.Properties.Count);
            Assert.Equal(new string('X', Property.MaxDictionaryNameLength), telemetry.Properties.Keys.ToArray()[0]);
            Assert.Equal(new string('X', Property.MaxValueLength), telemetry.Properties.Values.ToArray()[0]);
            Assert.Equal(new string('X', Property.MaxDictionaryNameLength - 3) + "001", telemetry.Properties.Keys.ToArray()[1]);
            Assert.Equal(new string('X', Property.MaxValueLength), telemetry.Properties.Values.ToArray()[1]);

            Assert.Same(telemetry.Properties, telemetry.Properties);
        }

        [TestMethod]
        public void DependencyTelemetryImplementsISupportSamplingContract()
        {
            var telemetry = new DependencyTelemetry();

            Assert.NotNull(telemetry as ISupportSampling);
        }

        [TestMethod]
        public void DependencyTelemetryHasCorrectValueOfSamplingPercentageAfterSerialization()
        {
            var telemetry = this.CreateRemoteDependencyTelemetry("mycommand");
            ((ISupportSampling)telemetry).SamplingPercentage = 10;

            var item = TelemetryItemTestHelper.SerializeDeserializeTelemetryItem<DependencyTelemetry, AI.RemoteDependencyData>(telemetry);

            Assert.Equal(10, item.sampleRate);
        }

        private DependencyTelemetry CreateRemoteDependencyTelemetry()
        {
            DependencyTelemetry item = new DependencyTelemetry
                                            {                                              
                                                Timestamp = DateTimeOffset.Now,
                                                Sequence = "4:2",
                                                Name = "MyWebServer.cloudapp.net",
                                                Duration = TimeSpan.FromMilliseconds(42),
                                                Success = true,
                                                Id = "DepID",
                                                ResultCode = "200",
                                                DependencyTypeName = "SQL"
                                            };
            item.Context.InstrumentationKey = Guid.NewGuid().ToString();
            item.Properties.Add("TestProperty", "TestValue");

            return item;
        }

        private DependencyTelemetry CreateRemoteDependencyTelemetry(string commandName)
        {
            DependencyTelemetry item = this.CreateRemoteDependencyTelemetry();
            item.Data = commandName;
            return item;
        }
    }
}