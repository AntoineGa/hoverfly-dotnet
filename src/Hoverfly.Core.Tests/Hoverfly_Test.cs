﻿namespace Hoverfly.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using Configuration;

    using Core.Dsl;

    using Model;
    using Resources;

    using Xunit;

    using static Core.Dsl.HoverflyDsl;
    using static Core.Dsl.ResponseCreators;
    using static Core.Dsl.DslSimulationSource;

    public class Hoverfly_Test
    {
        //TODO: The tests are now working against an external source, which can be down or be gone. Need to create a own Web server for the test.

        private readonly string _hoverflyPath = Path.Combine(Environment.CurrentDirectory,"..\\..\\..\\packages\\SpectoLabs.Hoverfly.0.10.2\\tools\\");

        [Fact]
        public void ShouldReturnCorrectConfiguredProxyPort()
        {
            var config = HoverflyConfig.Config().SetProxyPort(8600);
            var hoverfly = new Hoverfly(HoverflyMode.Simulate, config);

            Assert.Equal(8600, hoverfly.GetProxyPort());
        }


        [Fact]
        public void ShouldReturnCorrectConfiguredAdminPort()
        {
            var config = HoverflyConfig.Config().SetAdminPort(8880);
            var hoverfly = new Hoverfly(HoverflyMode.Simulate, config);

            Assert.Equal(8880, hoverfly.GetAdminPort());
        }


        [Fact]
        public void ShouldReturnSimulateMode_WhenHoverFlyIsSetToUseSimulateMode()
        {
            using (var hoverfly = new Hoverfly(HoverflyMode.Simulate))
            {
                hoverfly.Start();
                Assert.Equal(HoverflyMode.Simulate, hoverfly.GetMode());
            }
        }

        [Fact]
        public void ShouldReturnCaptureMode_WhenHoverFlyIsSetToUseCaptureMode()
        {
            using (var hoverfly = new Hoverfly(HoverflyMode.Capture))
            {
                hoverfly.Start();
                Assert.Equal(HoverflyMode.Capture, hoverfly.GetMode());
            }
        }

        [Fact]
        public void ShouldReturnSimulateMode_WhenHoverFlyIsSetToUseWebserverMode()
        {
            // NOTE: Hoverfly instance doesn't return WebServer as mode, instead when
            // running as Webserver, the mode of the Hoverfly is Simulate.
            using (var hoverfly = new Hoverfly(HoverflyMode.WebServer))
            {
                hoverfly.Start();
                Assert.Equal(HoverflyMode.Simulate, hoverfly.GetMode());
            }
        }

        [Fact]
        public void ShouldExportSimulation()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);
            var hoverfly = new Hoverfly(HoverflyMode.Capture, config);

            hoverfly.Start();

            GetContentFrom("http://echo.jsontest.com/key/value/one/two");

            var destinatonSource = new FileSimulationSource("simulation.json");
            hoverfly.ExportSimulation(destinatonSource);

            hoverfly.Stop();
        }

        [Fact]
        public void ShouldReturnCorrectSimulationDataResult_WhenHoverflyInWebserverModeImportingSimulationData()
        {
            var result = GetContentFrom("http://echo.jsontest.com/key/value/one/two");

            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);
            var hoverfly = new Hoverfly(HoverflyMode.WebServer, config);

            hoverfly.Start();

            // Simulation_test.json holds a captured result from http://echo.jsontest.com/key/value/one/two
            hoverfly.ImportSimulation(new FileSimulationSource("simulation_test.json"));

            var result2 = GetContentFrom("http://localhost:8500/key/value/one/two");

            hoverfly.Stop();

            Assert.Equal(result, result2);
        }

        [Fact]
        public void ShouldReturnCorrectSimulationDataResult_WhenHoverflyInSimulationMode()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);
            var hoverfly = new Hoverfly(HoverflyMode.Simulate, config);

            hoverfly.Start();

            // The time.jsontest.com returns the current time and milliseconds from the server.
            var result = GetContentFrom("http://time.jsontest.com");

            Thread.Sleep(10);

            var result2 = GetContentFrom("http://time.jsontest.com");

            hoverfly.Stop();

            Assert.Equal(result, result2);
        }

        [Fact]
        public void ShouldReturnCorrectHoverflyMode()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);
            var hoverfly = new Hoverfly(HoverflyMode.Simulate, config);

            hoverfly.Start();

            var mode = hoverfly.GetMode();

            hoverfly.Stop();

            Assert.Equal(HoverflyMode.Simulate, mode);
        }

        [Fact]
        public void ShouldReturnCorrectMode_WhenHoverflyModeIsChanged()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);
            var hoverfly = new Hoverfly(HoverflyMode.Simulate, config);

            hoverfly.Start();

            hoverfly.ChangeMode(HoverflyMode.Capture);

            var mode = hoverfly.GetMode();

            hoverfly.Stop();

            Assert.Equal(HoverflyMode.Capture, mode);
        }

        [Fact]
        public void ShouldReturnSimluationFromHoverfly_WhenFileSimulationSourceIsUsed()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);
            var hoverfly = new Hoverfly(HoverflyMode.WebServer, config);

            hoverfly.Start();

            hoverfly.ImportSimulation(new FileSimulationSource("simulation_test.json"));

            var simulation = hoverfly.GetSimulation();

            hoverfly.Stop();

            var request = simulation.HoverflyData.RequestResponsePair.First().Request;
            var response = simulation.HoverflyData.RequestResponsePair.First().Response;

            Assert.Equal(request.Method, "GET");
            Assert.Equal(request.Path, "/key/value/one/two");
            Assert.Equal(request.Destination, "echo.jsontest.com");
            Assert.Equal(request.Scheme, "http");

            Assert.Equal(response.Status, 200);
            Assert.Equal(response.Body, "{\n   \"one\": \"two\",\n   \"key\": \"value\"\n}\n");
        }

        [Fact]
        public void ShouldReturnCorrectSimluationFromHoverfly_WhenImportingSimulation()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);
            var hoverfly = new Hoverfly(HoverflyMode.Simulate, config);

            hoverfly.Start();

            var simulation = CreateTestSimulation();

            hoverfly.ImportSimulation(simulation);

            var expectedSimulation = hoverfly.GetSimulation();

            hoverfly.Stop();

            var expectedRequest = expectedSimulation.HoverflyData.RequestResponsePair.First().Request;
            var expectedResponse = expectedSimulation.HoverflyData.RequestResponsePair.First().Response;

            Assert.Equal(expectedRequest.Method, "GET");
            Assert.Equal(expectedRequest.Path, "/key/value/three/four");
            Assert.Equal(expectedRequest.Destination, "echo.jsontest.com");
            Assert.Equal(expectedRequest.Scheme, "http");

            Assert.Equal(expectedResponse.Status, 200);
            Assert.Equal(expectedResponse.Body, "{\n   \"three\": \"four\",\n   \"key\": \"value\"\n}\n");
        }

        [Fact]
        public void ShouldReturnCorrectRestultFromARequest_WhenImportingSimulationAndUsingWebServerMode()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);
            var hoverfly = new Hoverfly(HoverflyMode.WebServer, config);

            hoverfly.Start();

            var simulation = CreateTestSimulation();

            hoverfly.ImportSimulation(simulation);

            var result = GetContentFrom("http://localhost:8500/key/value/three/four?name=test");

            hoverfly.Stop();

            Assert.Equal("{\n   \"three\": \"four\",\n   \"key\": \"value\"\n}\n",result);
        }

        [Fact]
        public void ShouldReturnCorrectRestultFromARequest_WhenImportingSimulationAndUsingSimulationMode()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);
            var hoverfly = new Hoverfly(HoverflyMode.Simulate, config);

            hoverfly.Start();

            var simulation = CreateTestSimulation();

            hoverfly.ImportSimulation(simulation);

            var result = GetContentFrom("http://echo.jsontest.com/key/value/three/four?name=test");

            hoverfly.Stop();

            Assert.Equal("{\n   \"three\": \"four\",\n   \"key\": \"value\"\n}\n", result);
        }

        [Fact]
        public void ShouldGetCorrectResponse_WhenUsingDsl()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);

            using (var hoverfly = new Hoverfly(HoverflyMode.Simulate, config))
            {
                hoverfly.Start();

                hoverfly.ImportSimulation(
                    DslSimulationSource.Dsl(
                        Service("http://echo.jsontest.com")
                            .Get("/key/value/three/four")
                            .QueryParam("name", "test")
                            .WillReturn(
                                Success("{\n   \"three\": \"four\",\n   \"key\": \"value\"\n}\n", "application/json"))));

                var result = GetContentFrom("http://echo.jsontest.com/key/value/three/four?name=test");

                Assert.Equal("{\n   \"three\": \"four\",\n   \"key\": \"value\"\n}\n", result);
            }
        }

        [Fact]
        public void ShouldUseRemoteHovervyInstance()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);
            using (var hoverfly = new Hoverfly(HoverflyMode.Simulate, config))
            {
                hoverfly.Start();

                hoverfly.ImportSimulation(
                    DslSimulationSource.Dsl(
                        Service("http://echo.jsontest.com")
                            .Get("/key/value/three/four")
                            .QueryParam("name", "test")
                            .WillReturn(
                                Success("{\n   \"three\": \"four\",\n   \"key\": \"value\"\n}\n", "application/json"))));

                var simulation = hoverfly.GetSimulation();
                
                var config2 = HoverflyConfig.Config().UseRemoteInstance(config.RemoteHost, config.ProxyPort, config.AdminPort);
                using (var reuseHoverfly = new Hoverfly(config: config2))
                {
                    var simulation2 = reuseHoverfly.GetSimulation();

                    Assert.Equal(hoverfly.GetAdminPort(), reuseHoverfly.GetAdminPort());
                    Assert.Equal(hoverfly.GetProxyPort(), reuseHoverfly.GetProxyPort());
                    Assert.Equal(simulation.HoverflyData.RequestResponsePair.First().Response.Body, simulation2.HoverflyData.RequestResponsePair.First().Response.Body);
                }
            }
        }

        [Fact]
        public void ShouldBeDelay_WhenAddingADelaryToRequestWithDsl()
        {
            var config = HoverflyConfig.Config().SetHoverflyBasePath(_hoverflyPath);

            using (var hoverfly = new Hoverfly(HoverflyMode.Simulate, config))
            {
                hoverfly.Start();

                hoverfly.ImportSimulation(
                    DslSimulationSource.Dsl(
                        Service("http://echo.jsontest.com")
                            .Get("/key/value/three/four")
                            .WithDelay(2000)
                            .WillReturn(Success("Test", "application/json"))));

                var stopWatch = Stopwatch.StartNew();
                GetContentFrom("http://echo.jsontest.com/key/value/three/four");
                stopWatch.Stop();

                Assert.Equal(true, stopWatch.Elapsed.TotalMilliseconds >= 2000);
            }
        }

        private static Simulation CreateTestSimulation()
        {
            return new Simulation(
                        new HoverflyData(
                            new List<RequestResponsePair> {
                                    new RequestResponsePair(
                                        new Request
                                        {
                                            Scheme = "http",
                                            Destination = "echo.jsontest.com",
                                            Method = "GET",
                                            Path = "/key/value/three/four",
                                            Query = "name=test",
                                            Headers = new Dictionary<string, IList<string>> { { "Content-Type", new List<string> { "application/json" } } }
                                        },
                                        new Response
                                        {
                                            Status = 200,
                                            Body = "{\n   \"three\": \"four\",\n   \"key\": \"value\"\n}\n",
                                            EncodedBody = false,
                                            Headers = new Dictionary<string, IList<string>> { { "Content-Type", new List<string> { "application/json" } } }
                                        })}, 
                            null),
                        new HoverflyMetaData());
        }

        private static string GetContentFrom(string url)
        {
            var response = Task.Run(() => new HttpClient().GetAsync(url)).Result;
            return Task.Run(() => response.Content.ReadAsStringAsync()).Result;
        }
    }
}