namespace Server.Endpoints
{
    public static class TestEndpoints
    {
        private static RouteGroupBuilder testApi;
        private static SqlContext dbContext;

        public static RouteGroupBuilder MapTestEndpoints(this RouteGroupBuilder group)
        {
            group.MapGet("/test", Test);
            group.MapGet("/testdb", TestDb);

            testApi = group;
            return group;
        }

        public static void Test()
        {
            //var testApi = app.MapGroup("/api/");

            testApi.MapGet("/test", () => new Response($"Current time is {DateTime.Now}"));
        }

        public static void TestDb()
        {
            //var testApi = app.MapGroup("/api/");

            testApi.MapGet("/testdb", () => new Response($"{dbContext.TestModels.Count()}"));
        }
    }
}
