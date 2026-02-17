using FluentAssertions;
using Techbuilder.DeclareAPI.Core.Configuration;

namespace Techbuilder.DeclareAPI.Tests;

public class AuthorizationConfigTests
{
    [Fact]
    public void EndpointConfig_WithAuthorize_RequiresAuthorization()
    {
        // Arrange & Act
        var endpoint = new EndpointConfig
        {
            Method = "GET",
            Path = "/test",
            Authorize = true
        };

        // Assert
        endpoint.RequiresAuthorization.Should().BeTrue();
    }

    [Fact]
    public void EndpointConfig_WithPolicy_RequiresAuthorization()
    {
        // Arrange & Act
        var endpoint = new EndpointConfig
        {
            Method = "GET",
            Path = "/test",
            Policy = "admin"
        };

        // Assert
        endpoint.RequiresAuthorization.Should().BeTrue();
    }

    [Fact]
    public void EndpointConfig_WithRoles_RequiresAuthorization()
    {
        // Arrange & Act
        var endpoint = new EndpointConfig
        {
            Method = "GET",
            Path = "/test",
            Roles = new List<string> { "admin", "manager" }
        };

        // Assert
        endpoint.RequiresAuthorization.Should().BeTrue();
    }

    [Fact]
    public void EndpointConfig_WithoutAuth_DoesNotRequireAuthorization()
    {
        // Arrange & Act
        var endpoint = new EndpointConfig
        {
            Method = "GET",
            Path = "/test"
        };

        // Assert
        endpoint.RequiresAuthorization.Should().BeFalse();
    }

    [Fact]
    public void EndpointConfig_EmptyRoles_DoesNotRequireAuthorization()
    {
        // Arrange & Act
        var endpoint = new EndpointConfig
        {
            Method = "GET",
            Path = "/test",
            Roles = new List<string>()
        };

        // Assert
        endpoint.RequiresAuthorization.Should().BeFalse();
    }
}

public class CacheConfigTests
{
    [Fact]
    public void CacheConfig_Defaults_AreCorrect()
    {
        // Arrange & Act
        var config = new CacheConfig();

        // Assert
        config.Duration.Should().Be(60);
        config.VaryByQuery.Should().BeTrue();
        config.VaryByUser.Should().BeFalse();
        config.VaryByParams.Should().BeNull();
    }

    [Fact]
    public void CacheConfig_CanSetAllProperties()
    {
        // Arrange & Act
        var config = new CacheConfig
        {
            Duration = 300,
            VaryByQuery = false,
            VaryByUser = true,
            VaryByParams = new List<string> { "page", "pageSize" }
        };

        // Assert
        config.Duration.Should().Be(300);
        config.VaryByQuery.Should().BeFalse();
        config.VaryByUser.Should().BeTrue();
        config.VaryByParams.Should().Contain("page");
        config.VaryByParams.Should().Contain("pageSize");
    }
}

public class RateLimitConfigTests
{
    [Fact]
    public void RateLimitConfig_Defaults_AreCorrect()
    {
        // Arrange & Act
        var config = new RateLimitConfig();

        // Assert
        config.Limit.Should().Be(100);
        config.Window.Should().Be(60);
        config.Policy.Should().BeNull();
    }

    [Fact]
    public void RateLimitConfig_CanSetAllProperties()
    {
        // Arrange & Act
        var config = new RateLimitConfig
        {
            Limit = 50,
            Window = 30,
            Policy = "premium"
        };

        // Assert
        config.Limit.Should().Be(50);
        config.Window.Should().Be(30);
        config.Policy.Should().Be("premium");
    }
}

public class ConfigLoaderAuthorizationTests
{
    [Fact]
    public void LoadFromYaml_WithAuthorization_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
database:
  provider: postgresql
  connection: test
entities:
  Patient:
    endpoints:
      list:
        method: GET
        path: /patients
        source:
          type: view
          name: vw_patients
        authorize: true
      admin:
        method: DELETE
        path: /patients/{id}
        source:
          type: procedure
          name: sp_delete_patient
        policy: admin_only
        params:
          - name: id
            type: uuid
            from: route
      roles_endpoint:
        method: PUT
        path: /patients/{id}
        source:
          type: procedure
          name: sp_update_patient
        roles:
          - admin
          - manager
        params:
          - name: id
            type: uuid
            from: route
";
        var loader = new ConfigLoader();

        // Act
        var config = loader.LoadFromYaml(yaml);

        // Assert
        var endpoints = config.Entities["Patient"].Endpoints;

        endpoints["list"].Authorize.Should().BeTrue();
        endpoints["list"].RequiresAuthorization.Should().BeTrue();

        endpoints["admin"].Policy.Should().Be("admin_only");
        endpoints["admin"].RequiresAuthorization.Should().BeTrue();

        endpoints["roles_endpoint"].Roles.Should().Contain("admin");
        endpoints["roles_endpoint"].Roles.Should().Contain("manager");
        endpoints["roles_endpoint"].RequiresAuthorization.Should().BeTrue();
    }

    [Fact]
    public void LoadFromYaml_WithCacheConfig_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
database:
  provider: postgresql
  connection: test
entities:
  Patient:
    endpoints:
      list:
        method: GET
        path: /patients
        source:
          type: view
          name: vw_patients
        cache:
          duration: 300
          vary_by_query: true
          vary_by_user: true
";
        var loader = new ConfigLoader();

        // Act
        var config = loader.LoadFromYaml(yaml);

        // Assert
        var cache = config.Entities["Patient"].Endpoints["list"].Cache;
        cache.Should().NotBeNull();
        cache!.Duration.Should().Be(300);
        cache.VaryByQuery.Should().BeTrue();
        cache.VaryByUser.Should().BeTrue();
    }

    [Fact]
    public void LoadFromYaml_WithRateLimitConfig_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
database:
  provider: postgresql
  connection: test
entities:
  Patient:
    endpoints:
      list:
        method: GET
        path: /patients
        source:
          type: view
          name: vw_patients
        rate_limit:
          limit: 50
          window: 30
          policy: api_limit
";
        var loader = new ConfigLoader();

        // Act
        var config = loader.LoadFromYaml(yaml);

        // Assert
        var rateLimit = config.Entities["Patient"].Endpoints["list"].RateLimit;
        rateLimit.Should().NotBeNull();
        rateLimit!.Limit.Should().Be(50);
        rateLimit.Window.Should().Be(30);
        rateLimit.Policy.Should().Be("api_limit");
    }
}
