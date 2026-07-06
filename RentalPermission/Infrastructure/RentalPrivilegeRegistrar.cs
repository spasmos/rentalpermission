using Vintagestory.API.Server;

namespace RentalPermission;

internal static class RentalPrivilegeRegistrar
{
    public static void Register(ICoreServerAPI api, RentalPermissionConfig config)
    {
        foreach (RentalClaimRule claimRule in config.Claims)
        {
            foreach (RentalRule rule in claimRule.Rules)
            {
                if (rule.AllowDelegatedRental && !string.IsNullOrWhiteSpace(rule.RentalPrivilegeCode))
                {
                    api.Permissions.RegisterPrivilege(rule.RentalPrivilegeCode, "Allows renting configured reinforced or locked blocks inside claims", false);
                }
            }
        }
    }
}
