using KargoApi;
using KargoApi.Data;
using KargoApi.Data.Extensions;
using KargoApi.Features.Branches;
using KargoApi.Features.Shipments;
using KargoApi.Search;
using KargoApi.Search.Extensions;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPersistence(builder.Configuration, builder.Environment)
    .AddSearch(builder.Configuration)
    .ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));


var app = builder.Build();

if (app.Environment.IsDevelopment())
    await app.InitializeDevelopmentAsync();

app.MapBranchEndpoints();
app.MapShipmentEndpoints();
app.MapSearchEndpoints();

app.Run();