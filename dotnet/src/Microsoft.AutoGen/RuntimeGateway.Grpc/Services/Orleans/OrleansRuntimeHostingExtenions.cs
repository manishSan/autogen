// Copyright (c) Microsoft Corporation. All rights reserved.
// OrleansRuntimeHostingExtenions.cs

using System.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Serialization;

namespace Microsoft.AutoGen.RuntimeGateway.Grpc;

public static class OrleansRuntimeHostingExtenions
{
    public static WebApplicationBuilder AddOrleans(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerializer(serializer => serializer.AddProtobufSerializer());

        // Ensure Orleans is added before the hosted service to guarantee that it starts first.
        //TODO: make all of this configurable
        builder.UseOrleans((siloBuilder) =>
        {
            // Development mode or local mode uses in-memory storage and streams
            if (builder.Environment.IsDevelopment())
            {
                siloBuilder.UseLocalhostClustering()
                       .AddMemoryStreams("StreamProvider")
                       .AddMemoryGrainStorage("PubSubStore")
                       .AddMemoryGrainStorage("AgentRegistryStore")
                       .AddMemoryGrainStorage("AgentStateStore");

                siloBuilder.UseInMemoryReminderService();
                siloBuilder.UseDashboard(x => x.HostSelf = true);

                siloBuilder.UseInMemoryReminderService();
            }
            else
            {
                var cosmosDbconnectionString = builder.Configuration.GetValue<string>("Orleans:CosmosDBConnectionString") ??
                    throw new ConfigurationErrorsException(
                        "Orleans:CosmosDBConnectionString is missing from configuration. This is required for persistence in production environments.");

                siloBuilder.Configure<SiloMessagingOptions>(options =>
                {
                    options.ResponseTimeout = TimeSpan.FromMinutes(3);
                    options.SystemResponseTimeout = TimeSpan.FromMinutes(3);
                });
                siloBuilder.Configure<ClientMessagingOptions>(options =>
                {
                    options.ResponseTimeout = TimeSpan.FromMinutes(3);
                });
                siloBuilder.UseCosmosClustering(o =>
                {
                    o.ConfigureCosmosClient(cosmosDbconnectionString);
                    o.ContainerName = "AutoGen";
                    o.DatabaseName = "clustering";
                    o.IsResourceCreationEnabled = true;
                });

                siloBuilder.UseCosmosReminderService(o =>
                {
                    o.ConfigureCosmosClient(cosmosDbconnectionString);
                    o.ContainerName = "AutoGen";
                    o.DatabaseName = "reminders";
                    o.IsResourceCreationEnabled = true;
                });
                siloBuilder.AddCosmosGrainStorage(
                    name: "AgentStateStore",
                    configureOptions: o =>
                    {
                        o.ConfigureCosmosClient(cosmosDbconnectionString);
                        o.ContainerName = "AutoGen";
                        o.DatabaseName = "persistence";
                        o.IsResourceCreationEnabled = true;
                    });
                //TODO: replace with EventHub
                siloBuilder
              .AddMemoryStreams("StreamProvider")
              .AddMemoryGrainStorage("PubSubStore");
            }
        });

        return builder;
    }
}
