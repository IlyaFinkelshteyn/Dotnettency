using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Dotnettency;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Sample.Pages
{
    public class Startup
    {
        [Obsolete]
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var defaultServices = services.Clone();

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
            });

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddMultiTenancy<Tenant>((builder) =>
             {
                 builder.IdentifyTenantsWithRequestAuthorityUri()
                        .InitialiseTenant<TenantShellFactory>()
                        .AddAspNetCore()
                        .ConfigureTenantFileProviders((hostingOptions) =>
                        {
                            var hostWebRootFileProvider = Environment.WebRootFileProvider;
                            hostingOptions.ConfigureTenantWebRootFileProvider(Environment.WebRootPath, (webRootOptions) =>
                            {
                                // WE use the tenant's guid id to partition one tenants files from another on disk.
                                Guid tenantGuid = (webRootOptions.Tenant?.TenantGuid).GetValueOrDefault();
                                webRootOptions.TenantPartitionId(tenantGuid)
                                                   .AllowAccessTo(hostWebRootFileProvider); // We allow the tenant web root file provider to access the environments web root files.
                            }, fp =>
                            {
                                // The file provider we add here, is one that dynamically switches based on the active tenant partition configuration above.
                                Environment.WebRootFileProvider = new CompositeFileProvider(new[] { fp, hostWebRootFileProvider });
                            });

                            var hostContentRootFileProvider = Environment.ContentRootFileProvider;
                            hostingOptions.ConfigureTenantContentFileProvider(Environment.ContentRootPath, (contentRootOptions) =>
                            {
                                // WE use the tenant's guid id to partition one tenants files from another on disk.
                                Guid tenantGuid = (contentRootOptions.Tenant?.TenantGuid).GetValueOrDefault();
                                contentRootOptions.TenantPartitionId(tenantGuid)
                                                   .AllowAccessTo(hostContentRootFileProvider); // We allow the tenant web root file provider to access the environments web root files.
                            }, fp =>
                            {
                                // The file provider we add here, is one that dynamically switches based on the active tenant partition configuration above.
                                Environment.ContentRootFileProvider = new CompositeFileProvider(new[] { fp, hostContentRootFileProvider });
                            });

                        })
                        .ConfigureTenantConfiguration((a) =>
                        {
                            var tenantConfig = new ConfigurationBuilder();
                            tenantConfig.AddJsonFile(Environment.ContentRootFileProvider, $"/appsettings.{a.Tenant?.Name}.json", true, true);
                            return tenantConfig;
                        })
                        .ConfigureTenantContainers((containerOptions) =>
                        {
                            containerOptions
                            .SetDefaultServices(defaultServices)
                            .AutofacAsync(async (tenantContext, tenantServices) =>
                            {
                                // Can now use tenant level configuration to decide how to bootstrap the tenants services here..
                                var currentTenantConfig = await tenantContext.GetConfiguration();
                                var someTenantConfigSetting = currentTenantConfig.GetValue<bool>("SomeSetting");
                                if(someTenantConfigSetting)
                                {
                                    // register services certain way for this tenant. 
                                }

                                if (tenantContext.Tenant != null)
                                {
                                    tenantServices.AddRazorPages((o) =>
                                    {
                                        o.RootDirectory = $"/Pages/{tenantContext.Tenant.Name}";
                                    }).AddNewtonsoftJson();
                                }
                            });
                        })
                        .ConfigureTenantMiddleware((tenantOptions) =>
                        {
                            tenantOptions.AspNetCorePipelineTask(async (context, tenantAppBuilder) =>
                            {
                                // Shows how you can access the current tenants configuration and use that when deciding
                                // how to configure the middleware pipeline for this particular tenant
                                var tenantConfig = await context.GetConfiguration(tenantAppBuilder.ApplicationServices);
                                var someTenantConfigSetting = tenantConfig.GetValue<bool>("SomeSetting");
                                if (someTenantConfigSetting)
                                {
                                    // register services certain way for this tenant. 
                                }

                                tenantAppBuilder.Use(async (c, next) =>
                                {
                                    Console.WriteLine("Entering tenant pipeline: " + context.Tenant?.Name);
                                    await next.Invoke();
                                });

                                tenantAppBuilder.UseRouting();

                                if (context.Tenant != null)
                                {
                                    tenantAppBuilder.UseAuthorization();

                                    tenantAppBuilder.Use(async (c, next) =>
                                    {
                                        // Demonstrates per tenant files.
                                        // /foo.txt exists for one tenant but not another.
                                        var webHostEnvironment = c.RequestServices.GetRequiredService<IWebHostEnvironment>();
                                        var contentFileProvider = webHostEnvironment.ContentRootFileProvider;
                                        var webFileProvider = webHostEnvironment.WebRootFileProvider;

                                        var fooTextFile = webFileProvider.GetFileInfo("/foo.txt");

                                        Console.WriteLine($"/Foo.txt file exists? {fooTextFile.Exists}");

                                        // Demonstrates per tenant config.
                                        // SomeSetting is true for Moogle tenant but not other tenants.                                       
                                        Console.WriteLine($"Tenant config setting: {someTenantConfigSetting}");

                                        await next.Invoke();
                                    });

                                    tenantAppBuilder.UseEndpoints(endpoints =>
                                    {
                                        endpoints.MapRazorPages();
                                    });
                                }
                            });
                        });

             });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMultitenancy<Tenant>((builder) =>
            {
                builder.UseTenantContainers()
                       .UsePerTenantMiddlewarePipeline(app);
            });
        }
    }
}
