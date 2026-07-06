using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RentalPermission;

internal sealed class RentalPositionResolver
{
    private readonly Func<ICoreServerAPI?> serverApi;

    public RentalPositionResolver(Func<ICoreServerAPI?> serverApi)
    {
        this.serverApi = serverApi;
    }

    public BlockPos Resolve(BlockPos pos)
    {
        return Resolve(pos, 0);
    }

    private BlockPos Resolve(BlockPos pos, int depth)
    {
        ICoreServerAPI? sapi = serverApi();
        if (sapi == null || depth >= 4)
        {
            return pos;
        }

        Block block = sapi.World.BlockAccessor.GetBlock(pos);
        if (TryGetMultiblockRoot(block, pos, out BlockPos rootPos))
        {
            Block rootBlock = sapi.World.BlockAccessor.GetBlock(rootPos);
            if (!SamePosition(pos, rootPos) && rootBlock.Id != 0)
            {
                return Resolve(rootPos, depth + 1);
            }
        }

        if (block is BlockDoor door && door.IsUpperHalf())
        {
            BlockPos lowerPos = pos.DownCopy(1);
            Block lowerBlock = sapi.World.BlockAccessor.GetBlock(lowerPos);
            if (lowerBlock is BlockDoor lowerDoor && !lowerDoor.IsUpperHalf())
            {
                return lowerPos;
            }
        }

        if (block is BlockBed && block.LastCodePart(1) == "feet")
        {
            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart(0));
            if (facing != null)
            {
                BlockPos headPos = pos.AddCopy(facing.Opposite);
                Block headBlock = sapi.World.BlockAccessor.GetBlock(headPos);
                if (headBlock is BlockBed && headBlock.LastCodePart(1) == "head")
                {
                    return headPos;
                }
            }
        }

        return pos;
    }

    private static bool TryGetMultiblockRoot(Block block, BlockPos pos, out BlockPos rootPos)
    {
        rootPos = pos;

        if (block.GetType().Name != "BlockMultiblock")
        {
            return false;
        }

        FieldInfo? offsetInvField = block.GetType().GetField("OffsetInv", BindingFlags.Instance | BindingFlags.Public);
        if (offsetInvField?.GetValue(block) is not Vec3i offsetInv)
        {
            return false;
        }

        rootPos = pos.AddCopy(offsetInv);
        return true;
    }

    private static bool SamePosition(BlockPos left, BlockPos right)
    {
        return left.X == right.X && left.Y == right.Y && left.Z == right.Z && left.dimension == right.dimension;
    }
}
