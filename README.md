# RentalPermission

`RentalPermission` adds paid rentals for reinforced or locked blocks inside configured land claims.

It is meant for administered servers where players can rent specific chests, doors, rooms, market containers or shared storage without granting broad build access or permanent ownership of public infrastructure.

## Summary

- Universal mod: install it on both server and client.
- Server-side authority decides the real rental and expiration result.
- Rules only apply inside matching claims.
- Each rule can use its own block filters, claim filters, price, duration and expiration action.
- Rentals use a configurable item currency, usually `game:gear-rusty`.
- Players confirm the rental in a client dialog before payment.
- This does not replace vanilla claims or reinforcement; it adds paid control on top of them.

## Current Scope

This mod is intended for server-administered rental areas.

Rules are defined in `ModConfig/rentalpermission.json`, so direct access to the server configuration files is required. Players can rent, list and renew their own rentals, but they do not create or manage rental rules themselves.

Typical use cases:

- market stalls with rented chests,
- inn rooms or apartments with rented doors,
- clan houses or public warehouses,
- city-managed storage areas,
- any controlled claim where players may reserve specific blocks but should not control the whole area.

## Configuration

The server creates the real config file at:

```text
ModConfig/rentalpermission.json
```

Commented templates are provided as references:

```text
rentalpermission.template.jsonc
rentalpermission.template.es.jsonc
```

Use the templates as documentation, but apply real changes to `ModConfig/rentalpermission.json`.

Recommended workflow:

1. Create a claim with a clear description.
2. Add a rental rule under `Rentals`.
3. Limit the rule with `AllowedClaimDescriptions` when possible.
4. Configure the block prefixes or exact block codes that can be rented.
5. Set price, duration and expiration action.
6. Grant the configured rental privilege, usually `rentblocks`.
7. Test with a non-admin player.
8. Use `/rentalpermission here` when a rental rule does not apply as expected.

Prefer claim descriptions over claim ids for stable setups:

- `AllowedClaimDescriptions` is recommended for towns, markets and rooms.
- `AllowedClaimIds` follows the visible `/land list` id and may change if claims are deleted or recreated.

Rental durations are defined per rule:

```jsonc
"RentDurationUnit": "days",
"RentDuration": 7,
"MinRentDuration": 7,
"RentDurationStep": 0
```

Supported units:

```text
hours
days
months
years
```

`BasePrice` is the price for the full configured `RentDuration`. Shorter selectable durations are calculated proportionally.

## Commands

Main command:

```text
/rentalpermission
```

Short alias:

```text
/rentperm
```

Player command:

```text
/rentalpermission mine
```

Admin commands require `controlserver`:

```text
/rentalpermission reload
/rentalpermission list
/rentalpermission cancel <rental id>
/rentalpermission process
/rentalpermission here
```

`/rentalpermission here` is the most useful config diagnostic command. It shows the claims at the admin player's current position and which RentalPermission claim rules match.

## Persistent Data

Configuration lives in `ModConfig`, but persistent mod data does not.

```text
ModData/<world-uid>/rentalpermission/rentalpermission.state.json
```

That file stores active and processed rental records. Back it up before manual edits.

## Usage Notes

- Grant the rental privilege only to roles that should be allowed to rent configured blocks.
- Keep rental areas narrow and explicit; broad claim rules can make public infrastructure hard to reason about.
- Start destructive expiration behavior with `WarnOnly` during testing.
- Test `RemoveReinforcement` and `UnlockAndRemoveReinforcement` in a staging world before production.
- Removing reinforcement can expose block contents to other players.
- Enable `MarketResetEnabled` only for tightly controlled CANMarket rental areas. The matching stall is resolved when the rental is created, and the scheduler later uses the stored stall position and block code when the rental expires.
- Use `/rentalpermission here` from inside the target claim when a player receives no rental prompt.

## Expiration Actions

Available `OnExpired` values:

- `WarnOnly`
- `UnlockOnly`
- `RemoveReinforcement`
- `UnlockAndRemoveReinforcement`

## Compatibility

- Client required, because rental confirmation uses a client UI.
- Designed to work alongside vanilla claims and reinforcement.
- Rental enforcement is attached to the vanilla reinforcement flow so delegated actions from other mods can also be charged.
- Separate from `claimactivitypermissions` by design.

## Changelog 1.1.0

In progress.

- Refactored the server mod system into focused services for commands, prompts, payments, persistence, expirations, claim matching, privilege registration and interaction flow.
- Refactored client-side prompt handling into smaller network and dialog components.
- Improved project documentation with a practical README.
- Removed the legacy release packaging script in favor of the standard build artifacts.
- Updated mod metadata and assembly version to `1.1.0`.
- Moved persistent rental records from `ModConfig/rentalpermission.data.json` to `ModData/<world-uid>/rentalpermission/rentalpermission.state.json`. This is not migrated automatically: server admins who want to keep existing rentals must manually copy the old file contents into the new state file and may remove the old file afterwards.
- Added optional CANMarket stall reset on rental expiration through `MarketResetEnabled`, `MarketStallBlockCodePrefixes`, `MarketStallBlockCodes`, `MarketStallSearchRadiusBlocks` and `MarketStallRequireUniqueMatch`.

## Changelog 1.0.0

- First public release.
- Added paid rental checks for reinforcing and locking configured blocks.
- Added configurable currency, claim filters, block filters, delegated rental privilege and optional vanilla `Use` access requirement.
- Added persistent rental records.
- Added player rental listing, in-world renewal and mandatory player-written rental descriptions.
- Added admin commands to reload config, list rentals, inspect claims, cancel records and process expirations.
- Added client-side rental confirmation UI with localized messages and translated currency names when available.
- Added automatic expiration processing, cleanup of processed records and expiration actions.
- Added duration configuration with `RentDurationUnit`, `RentDuration`, `MinRentDuration`, `RentDurationStep`, `DaysPerMonth` and `MonthsPerYear`.
- Added optional diagnostics through `LogIgnoredInteractions` and `/rentalpermission here`.
