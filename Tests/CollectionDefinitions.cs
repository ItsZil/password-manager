namespace Tests
{
    [CollectionDefinition(nameof(LoginRequestTests), DisableParallelization = true)]
    public class LoginRequestTests { }

    [CollectionDefinition(nameof(RegisterRequestTests), DisableParallelization = true)]
    public class RegisterRequestTests { }

    [CollectionDefinition(nameof(ServerCommunicationTests), DisableParallelization = true)]
    public class ServerCommunicationTests { }

    [CollectionDefinition(nameof(ConfigurationTests), DisableParallelization = true)]
    public class ConfigurationTests { }

    [CollectionDefinition(nameof(PasskeyTests), DisableParallelization = true)]
    public class PasskeyTests { }

    [CollectionDefinition(nameof(ExtraAuthTests), DisableParallelization = true)]
    public class ExtraAuthTests { }

    [CollectionDefinition(nameof(PinCodeTests), DisableParallelization = true)]
    public class PinCodeTests { }
}