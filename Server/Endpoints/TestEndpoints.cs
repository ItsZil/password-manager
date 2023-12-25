using Server.Models;

namespace Server.Endpoints
{
    internal static class TestEndpoints
    {
        internal static RouteGroupBuilder MapTestEndpoints(this RouteGroupBuilder group)
        {
            group.MapGet("/test", Test);
            group.MapGet("/testdb", TestDb);
            group.MapPost("/testcreatedbmodel", TestCreateDbModel);

            return group;
        }

        internal static IResult Test()
        {
            return Results.Ok(new Response($"Current time is {DateTime.Now}"));
        }

        internal static IResult TestDb(SqlContext dbContext)
        {
            if (!dbContext.TestModels.Any())
            {
                return Results.Ok(new Response("No TestModels in the database"));
            }
            var lastTestModel = dbContext.TestModels.OrderBy(t => t.Id).Last();
            return Results.Ok(lastTestModel);
        }

        internal static IResult TestCreateDbModel(SqlContext dbContext)
        {
            dbContext.TestModels.Add(new TestModel { Message = "Created in TestCreateDbModel"});
            dbContext.SaveChanges();

            return Results.Ok(new Response("Created a new TestModel in the database"));
        }
    }

    internal record Response(string Message);
}
