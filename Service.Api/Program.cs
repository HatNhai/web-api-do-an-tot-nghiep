using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Service.Infrastructure.Persistence;
using Asp.Versioning;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Service.Application.Interfaces;
using Service.Application.Services;
using Service.Domain;
using Service.Infrastructure;
using Service.Infrastructure.Services;
using Service.Shared.Commons.Interfaces.Extentions;
using Service.Shared.Commons.Services;
using Service.Api;
using System.Reflection;

// Bootstrap logger để bắt log ngay từ lúc start app (trước cả khi DI sẵn sàng)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Đang khởi động Service.Api...");

    var builder = WebApplication.CreateBuilder(args);

    // Đăng ký Serilog với DI: đọc cấu hình từ appsettings + ghi ra console
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddControllers();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("SQLConnection")));

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<IRequestContext, RequestContext>();

    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddSingleton<IMlPredictorService, MlPredictorService>();
    builder.Services.AddSingleton<ModelReadinessState>();
    builder.Services.AddScoped<IPredictService, PredictService>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "DOANTOTNGHIEP API",
            Version = "v1",
            Description = "API chẩn đoán bệnh lá cây (MLP + RF)"
        });

        foreach (var xmlFile in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.xml"))
        {
            try { options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true); }
            catch { /* skip xml files that are not doc files */ }
        }
    });

    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    var app = builder.Build();

    // Middleware log MỌI request: method, path, status code, thời gian xử lý.
    // Tự nâng level lên Warning (4xx) hoặc Error (5xx/exception) để dễ nhận biết.
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.0} ms) từ {RemoteIP}";
        options.GetLevel = (httpContext, elapsed, ex) =>
            ex != null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400 ? LogEventLevel.Warning
            : LogEventLevel.Information;
        options.EnrichDiagnosticContext = (diagCtx, httpCtx) =>
        {
            diagCtx.Set("RemoteIP", httpCtx.Connection.RemoteIpAddress?.ToString() ?? "-");
            diagCtx.Set("UserAgent", httpCtx.Request.Headers.UserAgent.ToString());
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "DOANTOTNGHIEP API v1");
            c.RoutePrefix = "swagger";
        });
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAuthorization();
    app.MapControllers();

    // Endpoint nhẹ để UI kiểm tra model đã sẵn sàng chưa.
    // KHÔNG phụ thuộc model nặng nên trả lời tức thì kể cả khi model đang nạp.
    app.MapGet("/api/health/ready", (ModelReadinessState state) =>
        Results.Ok(new { ready = state.IsReady, error = state.Error }));

    // Warm-up model ở LUỒNG NỀN: API khởi động & nhận request ngay lập tức, model nạp phía sau
    // (load 3 model ONNX + init ONNX Runtime/OpenCV + JIT pipeline). UI hiện màn loading và poll
    // /api/health/ready cho tới khi model sẵn sàng → tránh cold-start dồn vào request đầu gây timeout.
    var readiness = app.Services.GetRequiredService<ModelReadinessState>();
    _ = Task.Run(() =>
    {
        try
        {
            app.Services.GetRequiredService<IMlPredictorService>().Warmup();
            readiness.MarkReady();
            Log.Information("Model ML đã sẵn sàng phục vụ.");
        }
        catch (Exception ex)
        {
            readiness.MarkFailed(ex.Message);
            Log.Error(ex, "Warm-up model thất bại.");
        }
    });

    Log.Information("Service.Api đã khởi động (model đang nạp ở luồng nền). Swagger ở /swagger.");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service.Api dừng đột ngột do lỗi không xử lý được");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
