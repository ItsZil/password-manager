namespace Server.Endpoints
{
    public static class TestEndpoints
    {

        public static RouteGroupBuilder MapTestEndpoints(this RouteGroupBuilder group)
        {
            group.MapGet("/test", Test);
            group.MapGet("/testdb", TestDb);

            return group;
        }

        public static IResult Test()
        {
            return Results.Ok(new Response($"Current time is {DateTime.Now}"));
        }

        public static IResult TestDb(SqlContext dbContext)
        {
            return Results.Ok(new Response($"{dbContext.TestModels.Count()}"));
        }
    }

    public record Response(string Message);
}
