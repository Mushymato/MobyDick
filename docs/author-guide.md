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
| WiggleSegmentLength | int | `0` | If this is more than 0, slice the sprite up into segments of this length and wiggle them according to speed. This emulates how vanilla fish sprites "swim". |
| DrawScaleInTank | float | `4.0` | The draw scale of the fish when placed in tank. Can set a smaller number here but bigger sprite size for HD fish. |
| AquariumTextureOverride | string | _null_ | Replace the aquarium texture with texture at this path. You can specify the texture in `Data/AquariumFish`, but this provides a shortcut. |
| AquariumTextureRect | Rectangle | _empty_ | Decide which area of the aquarium texture to treat as the bounds of the texture in general, which is then used for frame calculations. If this is empty, then the texture's bounds is used. |
| AquariumTexturesConditional | List<MobyDickTextureConditionalData> | _null_ | List of conditional aquarium texture rectangles. |
| HeldItemOriginOffset | Vector2 | `0,0` | How much to offset when the fish is held over player's head as an item. |
| SwimAnimation | List<int> | _null_ | A special swimming animation, whose frame interval depends on the fish's movement speed. Fish animate faster when moving faster. |
| SwimAnimationInterval | float | 125f | The base frame interval for the swim animation. |
| SwimVelocityMin | float | `-1` | The minimum swim horizontal velocity. |
| SwimVelocityMax | Vector2 | `-1` | The maximum swim horizontal velocity. |
| SwimCooldownMin | float | `0` | The minimum time in seconds between swim attempts. |
| SwimCooldownMax | float | `0` | The maximum time in seconds between swim attempts. |
| MinimumVelocity | float | `-1` | If set to 0 or above, this will alter the minimum velocity of the fish. This affects how fast they move and how often they dart. |
| MinimumVelocityVariance | float | `0.1` | If MinimumVelocity will be altered, a random value between 0 and MinimumVelocityVariance is added to it. |

#### `MobyDickTextureConditionalData`

| Field | Type | Default | Description |
| ----- | ---- | ------- | ----------- |
| Id | string | `"MobyDickTextureConditionalData"` | Unique Id to identify this entry with. |
| TextureRect | Rectangle | _empty_ | Decide which area of the aquarium texture to treat as the bounds of the texture in general, which is then used for frame calculations. If this is empty, then the texture's bounds is used. |
| Season | Season | _null_ | Specify a season this texture should appear during. |
| Condition | string | _null_ | Specify [game state query](https://stardewvalleywiki.com/Modding:Game_state_queries) to check. |
| Precedence | int | _null_ | Control which order the conditions are checked in, lower is earlier. |

This model does not have a independent `Texture` field and the source rect simply points to different areas of the base aquarium texture conditionally. Use it in conjunction with `AquariumTextureOverride`.

### Fish Frenzy

You can spawn a fish frenzy (i.e. the thing where fish appear enmass in a bubble spot) in the current location using this trigger action:

```
mushymato.MobyDick_FishFrenzy [fishId] [X] [Y]
```

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

### Decorative Location Tanks via `mushymato.MobyDick/Tanks`

This is a way to create decorative fish tanks in the map. These are not furniture in any way and fish inside cannot be accessed by the player.

| Field | Type | Default | Description |
| ----- | ---- | ------- | ----------- |
| DrawInBackground | bool | `false` | Whether this tank should be drawn behind all map layers. |
| DrawBubbles | bool | `false` | Whether to show fish bubbles. |
| SortTileOffset | float | `0f` | When not DrawInBackground, adjust what layer this tank should be drawn at. Positive is "towards the back". |
| TankBounds | Rectangle | _empty_ | The overall bounds of the tank, affects positioning on map and draw layer. This value is in pixels, i.e. each tile is 64x64. |
| ForegroundTexture | string | _null_ | The texture to draw above fish, i.e. glass |
| ForegroundSourceRect | Rectangle | _empty_ | Source rect of the foreground, if empty then the whole texture is used. |
| ForegroundTargetRect | Rectangle | _empty_ | Target rect of the foreground, if empty then TankBounds is used. |
| BackgroundTexture | string | _null_ | The texture to draw behind fish, i.e. tank bottom |
| BackgroundSourceRect | Rectangle | _empty_ | Source rect of the background, if empty then the whole texture is used. |
| BackgroundTargetRect | Rectangle | _empty_ | Target rect of the background, if empty then TankBounds is used. |
| Fishes | TankFishSpawnData | _empty_ | List of [item spawn fields](https://stardewvalleywiki.com/Modding:Item_queries) that determine what fish can appear. |

#### `TankFishSpawnData`

Aside from the generic [item spawn fields](https://stardewvalleywiki.com/Modding:Item_queries) you can use:

| Field | Type | Default | Description |
| ----- | ---- | ------- | ----------- |
| Condition | string | _null_ | A [game state query](https://stardewvalleywiki.com/Modding:Game_state_queries) that controls whether this spawn data takes effect overall. |
| SearchMode | ItemQuerySearchMode | AllOfTypeItem | How to query for item, the relevant options is `"AllOfTypeItem"` and `"RandomOfTypeItem"`. |
| RepeatCount | int | 1 | How many times to repeat this particular query, rolling new results each time. |
| TakeCount | int | -1 | How many items to take from the result, or -1 to take all. When used with RepeatCount, this is the amount to take on each repeat. |

