namespace UserManagementApi.Models;

public class User
{
    public int Id { get; set; }
    required public string FirstName { get; set; } = string.Empty;
    required public string LastName { get; set; } = string.Empty;
    required public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}
