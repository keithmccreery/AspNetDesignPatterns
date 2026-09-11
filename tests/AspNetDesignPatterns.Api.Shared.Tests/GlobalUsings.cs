global using AspNetDesignPatterns.Api.Shared.Results;

// Mirrors AspNetDesignPatterns.Api.Shared/GlobalUsings.cs -- this is a plain class library
// (Microsoft.NET.Sdk), so it doesn't get the ASP.NET Core implicit usings the Web SDK
// generates automatically, but the fixtures under test need them.
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
