namespace rinha_api.DTO;

public record HealthcheckDTO(bool Failing, int MinResponseTime);