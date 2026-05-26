using System.Text;
using System.Text.Json.Serialization;
using Brs.Api.Middleware;
using Brs.Infrastructure;
using Brs.Infrastructure.Auth;
using Brs.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// причина: enum'ы сериализуем по имени (case-insensitive) — фронту удобнее
// передавать "Isbn"/"Author", чем магические числа, и Swagger показывает их понятнее
builder.Services.AddControllers().AddJsonOptions(opt =>
    opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true)));
builder.Services.AddEndpointsApiExplorer();

// причина: Swagger с поддержкой Bearer — чтобы из UI можно было вводить токен
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BRS API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header: введите 'Bearer {token}'"
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

builder.Services.AddInfrastructure(builder.Configuration);

// причина: in-memory кэш для автодополнения — снижает нагрузку на БД при наборе текста
builder.Services.AddMemoryCache();

// причина: фронт ходит на бэк с другого origin, разрешаем CORS только для конфигурируемых адресов
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// причина: настраиваем JWT bearer — параметры валидации совпадают с теми,
// что использовались при подписании в AuthService
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section не задана");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            // причина: убираем дефолтные 5 минут clock-skew, чтобы expiry было точным
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// причина: применяем миграции и сидируем данные при старте — упрощает первый запуск
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BrsDbContext>();
    await db.Database.MigrateAsync();
    await DataSeeder.SeedAsync(db);
}

// причина: ExceptionHandlingMiddleware ставим как можно раньше, чтобы ловить ошибки
// и от auth-pipeline, и от контроллеров
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors();

// причина: порядок UseAuthentication → UseAuthorization обязателен —
// сначала вытаскиваем identity из токена, потом проверяем права
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
