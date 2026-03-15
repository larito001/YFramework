# Framework Usage

## Overview
This project uses a small service-oriented runtime built around `GameContext`.

The layering rule is:

- `Framework`: generic runtime infrastructure
- `GamePlay`: project-specific services, scene definitions, UI registrations, and startup behavior

The framework should not hardcode current-project scene keys, UI enums, or gameplay manager registrations.

## Startup Flow
The runtime starts from:

- `Assets/Scripts/Framework/GameContext/GameLoop.cs`
- `Assets/Scripts/Framework/GameContext/GameBootstrapper.cs`
- `Assets/Scripts/GamePlay/GameProjectBootstrapper.cs`

Flow:

1. `GameLoop` builds the context through `GameBootstrapper.BuildContext()`
2. `GameContext.InitAll()` initializes registered services
3. `GameBootstrapper.RunStartup(ctx)` delegates project startup behavior to the gameplay partial bootstrapper

`GameBootstrapper` is the composition root. It is the correct place to:

- register framework services
- register the coroutine runner
- configure shared runtime helpers

`GameProjectBootstrapper` is the correct place to:

- register gameplay services
- register gameplay scenes
- register gameplay UI pages
- define the first screen or startup behavior

## Service Registration
Register services in `GameContext`:

```csharp
ctx.Register(new EventMgr());
ctx.Register(new UIMgr(BuildUiConfig()));
ctx.Register(new PlayerManager());
```

Supported lifecycle interfaces:

- `IGameService`: participates in `Init` / `Shutdown`
- `ITickable`: receives `Tick`
- `IFixedTickable`: receives `FixedTick`
- `ILateTickable`: receives `LateTick`

Guidelines:

- keep `Init` focused on dependency setup and subscriptions
- avoid spawning scene content inside `Init` unless the service is explicitly an object factory
- release subscriptions and runtime state in `Shutdown`

## Dependency Access
Prefer injected or cached dependencies over global lookups.

Recommended patterns:

- services: use `Init(GameContext ctx)`
- UI pages: use `Initialize(GameContext context, UIMgr manager)`
- scenes: use `Initialize(GameContext context)`
- plain C# objects created by managers: prefer constructor injection

Avoid:

- `GameLoop.Instance.Ctx.Get<T>()` in gameplay logic
- new global `Instance` singletons for services

Reasonable exception:

- `GameLoop.Instance` remains acceptable in the bootstrap path as the Unity host object

## Scenes
Scene flow is handled by:

- `GotSceneManager`
- `GotSceneBase`

Gameplay scenes should live in `GamePlay/Scene` and be registered from `GameProjectBootstrapper`.

Add a new scene:

1. create a class derived from `GotSceneBase`
2. implement `SceneType` and `SceneName`
3. register it in `ConfigureProjectScenes`

## UI
UI flow is handled by:

- `UIMgr`
- `UIPageHandler`
- `UIPageBase`
- `UIConfig`

Gameplay UI ids and registrations belong in the gameplay layer.

Add a new page:

1. add a value to the gameplay UI enum
2. create a `UIPageBase` subclass
3. register it in `ConfigureProjectUi`
4. add the prefab under the registered resource path

Page guidance:

- use `GetService<T>()` for runtime services
- use `CloseSelf()` instead of reaching into manager internals
- keep `OnLoad` for local page setup
- keep `BeforeShow` for per-open parameters

## Pooled Objects
Pooled object flow is handled by:

- `ObjectPool`
- `ObjectBase`

Create a pooled object by deriving from `ObjectBase` and implementing:

- `AfterInstanceGObj()`
- `BeforeRecover(bool isDelete)`

Typical usage:

```csharp
SetInVision(true);
SetPrefabBundlePath("Enemies/Enemy");
InstanceGObj();
```

## Runtime Config
`GameRuntimeConfig` carries startup-time data that used to live directly on `GameLoop`.

Use it for:

- test flags
- buff config assets
- other startup-only runtime settings

## Placeholder Scripts
Some gameplay pages are intentionally registered as placeholders so prefab routes stay valid.

When implementing a placeholder:

- keep the existing class and prefab path
- add real data binding and button handling
- remove the placeholder summary once the page has meaningful behavior

If a script is fully unused, prefer deleting it instead of leaving a commented-out implementation in the tree.

## Maintenance Rules
Use these rules going forward:

- do not add new service singletons for objects already owned by `GameContext`
- do not leave fully commented-out classes in source control
- move project enums and keys into `GamePlay`, not `Framework`
- keep comments short, readable, and in one language
- when a script is a placeholder, say so explicitly in a class summary
