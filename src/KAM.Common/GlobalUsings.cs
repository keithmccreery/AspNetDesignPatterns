// The Result / Error vocabulary is used by nearly every file in this project.
global using KAM.Common.Results;

// This is a plain class library (Microsoft.NET.Sdk), so it doesn't get the ASP.NET Core
// implicit usings the Web SDK generates automatically for AspNetDesignPatterns.Api (see its
// own GlobalUsings.cs) -- replicated here because nearly every file in this project needs them.
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
