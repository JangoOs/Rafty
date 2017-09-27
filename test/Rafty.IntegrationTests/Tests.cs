﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Rafty.Concensus;
using Shouldly;
using Xunit;

namespace Rafty.IntegrationTests
{
    public class Tests : IDisposable
    {
        private List<IWebHost> _builders;
        private List<IWebHostBuilder> _webHostBuilders;

        public Tests()
        {
            _webHostBuilders = new List<IWebHostBuilder>();
            _builders = new List<IWebHost>();
        }
        public void Dispose()
        {
            foreach (var builder in _builders)
            {
                builder.Dispose();
            }
        }

        [Fact]
        public void ShouldDoSomething()
        {
            var bytes = File.ReadAllText("peers.json");
            var peers = JsonConvert.DeserializeObject<FilePeers>(bytes);

            foreach (var peer in peers.Peers)
            {
                GivenAServerIsRunning(peer.HostAndPort, peer.Id);
            }

            var stopWatch = Stopwatch.StartNew();
            while (stopWatch.ElapsedMilliseconds < 25000)
            {

            }

            var p = peers.Peers.First();
            var command = new FakeCommand("WHATS UP DOC?");
            var json = JsonConvert.SerializeObject(command);
            var httpContent = new StringContent(json);
            using(var httpClient = new HttpClient())
            {
                var response = httpClient.PostAsync($"{p.HostAndPort}/command", httpContent).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var result = JsonConvert.DeserializeObject<Response<FakeCommand>>(content);
                result.Success.ShouldBeTrue();
            }
        }

        private void GivenAServerIsRunning(string url, string id)
        {
            var guid = Guid.Parse(id);

            IWebHostBuilder webHostBuilder = new WebHostBuilder();
            webHostBuilder.UseUrls(url)
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureServices(x =>
                {
                    x.AddSingleton(webHostBuilder);
                    x.AddSingleton(new NodeId(guid));
                })
                .UseStartup<Startup>();

            var builder = webHostBuilder.Build();
            builder.Start();

            _webHostBuilders.Add(webHostBuilder);
            _builders.Add(builder);
        }
    }

    public class FakeCommand
    {
        public FakeCommand(string value)
        {
            this.Value = value;

        }
        public string Value { get; private set; }
    }
}