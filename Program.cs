using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.GetRequiredSection("Mock").Get<MockSettings>()
    ?? throw new InvalidOperationException("Falta la configuración Mock.");
var app = builder.Build();

app.MapPost("/api/v1/auth", async (HttpRequest request) =>
{
    var delayError = await ApplyDelay(request, settings);
    if (delayError is not null) return delayError;

    if (request.Headers["Usuario"] != settings.Usuario || request.Headers["Llave"] != settings.Llave)
        return Message(StatusCodes.Status401Unauthorized, "Credenciales inválidas");

    AuthRequest? body;
    try { body = await request.ReadFromJsonAsync<AuthRequest>(); }
    catch (JsonException) { return Message(StatusCodes.Status400BadRequest, "Formato inválido en campo: cuerpo"); }

    if (string.IsNullOrWhiteSpace(body?.IdConsumidor))
        return Message(StatusCodes.Status400BadRequest, "Campo requerido faltante: id_consumidor");

    var authorization = request.Headers.Authorization.ToString();
    if (!string.IsNullOrEmpty(authorization))
    {
        var token = BearerToken(authorization);
        if (token == settings.ExpiredToken) return Message(StatusCodes.Status401Unauthorized, "Token expirado");
        if (token != settings.AccessToken) return Message(StatusCodes.Status401Unauthorized, "Token inválido");
    }

    return Results.Json(new { acceso_token = settings.AccessToken, expiracion_token = "60", mensaje = "Satisfactorio" });
});

app.MapPost("/productos/v1/posicionconsolidada", async (HttpRequest request) =>
{
    var delayError = await ApplyDelay(request, settings);
    if (delayError is not null) return delayError;

    var authorization = request.Headers.Authorization.ToString();
    if (string.IsNullOrEmpty(authorization)) return Message(StatusCodes.Status401Unauthorized, "Token no proporcionado");

    var token = BearerToken(authorization);
    if (token == settings.ExpiredToken) return Message(StatusCodes.Status401Unauthorized, "Token expirado");
    if (token != settings.AccessToken) return Message(StatusCodes.Status401Unauthorized, "Token inválido");

    PositionRequest? body;
    try { body = await request.ReadFromJsonAsync<PositionRequest>(); }
    catch (JsonException) { return Message(StatusCodes.Status400BadRequest, "Formato inválido en campo: cuerpo"); }

    var validation = Validate(body);
    if (validation is not null) return Message(StatusCodes.Status400BadRequest, validation);

    var identification = body!.Cliente!.Identificacion!.Numero!;
    if (identification == "00100000500") return Message(StatusCodes.Status500InternalServerError, "Error interno del servicio");
    if (identification == "00100000504")
    {
        await Task.Delay(settings.TimeoutDelayMs, request.HttpContext.RequestAborted);
        return Message(StatusCodes.Status504GatewayTimeout, "Tiempo de espera agotado");
    }

    var fixturePath = Path.Combine(app.Environment.ContentRootPath, "fixtures", $"{identification}.json");
    if (!File.Exists(fixturePath)) return Message(StatusCodes.Status404NotFound, "Cliente no encontrado");

    JsonObject? fixture;
    try { fixture = JsonNode.Parse(await File.ReadAllTextAsync(fixturePath, request.HttpContext.RequestAborted)) as JsonObject; }
    catch (JsonException) { return Message(StatusCodes.Status500InternalServerError, "Error interno del servicio"); }
    if (fixture is null) return Message(StatusCodes.Status500InternalServerError, "Error interno del servicio");

    fixture["id_consumidor"] = body.IdConsumidor;
    fixture["usuario"] = body.Usuario;
    fixture["terminal"] = body.Terminal;
    fixture["version"] = body.Version;
    fixture["fechaHora"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    fixture["TRNID"] = Random.Shared.NextInt64(100_000_000_000, 1_000_000_000_000).ToString(CultureInfo.InvariantCulture);
    return Results.Json(fixture);
});

app.Run();

static IResult Message(int statusCode, string mensaje) => Results.Json(new { mensaje }, statusCode: statusCode);

static async Task<IResult?> ApplyDelay(HttpRequest request, MockSettings settings)
{
    var value = request.Headers["X-Mock-Delay-Ms"].ToString();
    if (string.IsNullOrEmpty(value)) return null;
    if (!int.TryParse(value, out var milliseconds) || milliseconds < 0 || milliseconds > settings.MaxDelayMs)
        return Message(StatusCodes.Status400BadRequest, "Formato inválido en campo: X-Mock-Delay-Ms");
    await Task.Delay(milliseconds, request.HttpContext.RequestAborted);
    return null;
}

static string? BearerToken(string authorization) => authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
    ? authorization[7..].Trim()
    : null;

static string? Validate(PositionRequest? request)
{
    if (request is null) return "Campo requerido faltante: cuerpo";
    if (string.IsNullOrWhiteSpace(request.IdConsumidor)) return "Campo requerido faltante: id_consumidor";
    if (string.IsNullOrWhiteSpace(request.Usuario)) return "Campo requerido faltante: usuario";
    if (string.IsNullOrWhiteSpace(request.Terminal)) return "Campo requerido faltante: terminal";
    if (string.IsNullOrWhiteSpace(request.FechaHora)) return "Campo requerido faltante: fechaHora";
    if (!DateTime.TryParseExact(request.FechaHora, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        return "Formato inválido en campo: fechaHora";
    if (request.Version is null) return "Campo requerido faltante: version";
    if (string.IsNullOrWhiteSpace(request.Cliente?.Identificacion?.Numero)) return "Campo requerido faltante: cliente.identificacion.numero";
    if (string.IsNullOrWhiteSpace(request.Cliente?.Identificacion?.Tipo)) return "Campo requerido faltante: cliente.identificacion.tipo";
    return request.Cliente.Identificacion.Tipo is "Cedula" or "Pasaporte" or "RNC"
        ? null
        : "Formato inválido en campo: cliente.identificacion.tipo";
}

sealed class MockSettings
{
    public required string Usuario { get; init; }
    public required string Llave { get; init; }
    public required string AccessToken { get; init; }
    public required string ExpiredToken { get; init; }
    public int TimeoutDelayMs { get; init; }
    public int MaxDelayMs { get; init; }
}

sealed record AuthRequest([property: JsonPropertyName("id_consumidor")] string? IdConsumidor);
sealed record PositionRequest(
    [property: JsonPropertyName("id_consumidor")] string? IdConsumidor,
    [property: JsonPropertyName("usuario")] string? Usuario,
    [property: JsonPropertyName("terminal")] string? Terminal,
    [property: JsonPropertyName("fechaHora")] string? FechaHora,
    [property: JsonPropertyName("version")] int? Version,
    [property: JsonPropertyName("cliente")] Cliente? Cliente);
sealed record Cliente([property: JsonPropertyName("identificacion")] Identificacion? Identificacion);
sealed record Identificacion(
    [property: JsonPropertyName("numero")] string? Numero,
    [property: JsonPropertyName("tipo")] string? Tipo);
