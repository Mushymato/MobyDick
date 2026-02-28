## Moby Dick

Before you can do any moby dick related things, the fish must exist.

For a modded fish, you need to [add a fish with content patcher](https://stardewmodding.wiki.gg/wiki/Tutorial:_Adding_a_New_Fish).

After that, you can add Moby Dick specific data with content patcher.

### Bigger Fish via `mushymato.MobyDick/Data`

The key is the (unqualified) item id of the fish.

| Field | Type | Default | Description |
| ----- | ---- | ------- | ----------- |
| SpriteSize | Point | `24,24` | Size of each animation frame. All frame calculations use this size, including the animations from `Data/AquariumFish` as well as SwimAnimation on this asset. |
| RotateByVelocity | bool | `false` | Whether the sprite should tilt forward when they move fast. This emulates floater behavior. |
| WiggleSegmentLength | int | `0` | If this is more than 0, slice the sprite up into segments of this lenght and wiggle them according to speed. This emulates how vanilla fish sprites "swim". |
| DrawScaleInTank | float | `4.0` | The draw scale of the fish when placed in tank. Can set a smaller number here but bigger sprite size for HD fish. |
| AquariumTextureOverride | string | _null_ | Replace the aquarium texture with texture at this path. You can specify the texture in `Data/AquariumFish`, but this provides a shortcut. |
| AquariumTextureRect | Rectangle | _empty_ | Decide which area of the aquarium texture to treat as the bounds of the texture in general, which is then used for frame calculations. If this is empty, then the texture's bounds is used. |
| HeldItemOriginOffset | Vector2 | `0,0` | How much to offset when the fish is held over player's head as an item. |
| SwimAnimation | List<int> | _null_ | A special swimming animation, whose frame interval depends on the fish's movement speed. Fish animate faster when moving faster. |
| SwimAnimationInterval | float | 125f | The base frame interval for the swim animation. |

### Custom Bait and Tackle Items

In the base game you can use category -22 items as tackles and category -21 items as bait.

Moby Dick adds the ability to use objects of other categories as bait and tackle, plus some GSQ to help check that your fishing rod has that tackle equipped.

#### Bait

To make a object acceptable as bait for most fishing rods:
- Add context tag `"mobydick_bait_item"`

To make a fishing rod accept only specific context tag(s) as bait:
- Add custom field `"mushymato.MobyDick/BaitContextTag": "your_tags,separated_by_comma"` to the fishing rod's tool data.

To check that you have a particular bait equipped:
- Use GSQ `"mushymato.MobyDick_HAS_BAIT <baitId> [baitPreserveId]"`

#### Tackle

To make a object acceptable as bait for most fishing rods:
- Add context tag `"mobydick_tackle_item"`

To make a fishing rod accept only specific context tag(s) as bait:
- Add custom field `"mushymato.MobyDick/TackleContextTag": "your_tags,separated_by_comma"` to the fishing rod's tool data.

To check that you have a particular bait equipped:
- Use GSQ `"mushymato.MobyDick_HAS_TACKLE <baitId> [baitPreserveId]"`
