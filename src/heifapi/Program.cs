using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

LibHeifSharpSamples.LibHeifSharpDllImportResolver.Register();


var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();

var sampleTodos = new Todo[] {
    new(1, "Walk the dog"),
    new(2, "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
    new(3, "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
    new(4, "Clean the bathroom"),
    new(5, "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
};

{
    var todosApi = app.MapGroup("/todos");
    todosApi.MapGet("/", () => sampleTodos);
    todosApi.MapGet("/{id}", (int id) =>
        sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
            ? Results.Ok(todo)
            : Results.NotFound());
}


{
    var heifApi = app.MapGroup("/api/heif");
    heifApi.MapGet("/info", (string path) =>
    {
        return HeifInfo.Execute(path);
    });
    heifApi.MapGet("/convert", (string path) =>
    {
        var id = Guid.NewGuid().ToString();
        var filename = $"{id}.png";
        var dir = Path.Combine(Path.GetTempPath(), "heifapi");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var output = Path.Combine(dir, filename);
        HeifDec.Execute(path, output, extractPrimaryImage: true);

        var imageFile = Directory.GetFiles(dir, $"*{id}*")?.FirstOrDefault();
        if (imageFile != null)
        {
            var fi = new FileInfo(output);
            if (fi.Exists && fi.Length > 0)
            {
                return fi.FullName;
            }
        }
        return string.Empty;
    });
}


app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
