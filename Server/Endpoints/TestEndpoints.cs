namespace Server.Endpoints
{
    internal static class TestEndpoints
    {
        internal static RouteGroupBuilder MapTestEndpoints(this RouteGroupBuilder group)
        {
            group.MapGet("/test", Test);
            group.MapGet("/testdb", TestDb);

            return group;
        }

        internal static IResult Test()
        {
            return Results.Ok(new Response($"Current time is {DateTime.Now}"));
        }

        internal static IResult TestDb(SqlContext dbContext)
        {
            return Results.Ok(new Response($"{dbContext.TestModels.Count()}"));
        }
    }

    internal record Response(string Message);
}
