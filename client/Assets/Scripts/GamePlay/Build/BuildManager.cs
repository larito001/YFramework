using UnityEngine;

/// <summary>
/// 输入
/// </summary>
public class BuildManager:IGameService,ITickable
{
    TowerSystem towerSystem;
    public Camera cam;
    public LayerMask groundMask;
    public LayerMask blockingMask;

    public Material validMat;
    public Material invalidMat;

    public TowerConfigSO currentTower;
    public float rayDist = 200f;

    private BuildPlacementSystem _placement;


    public IInventoryService inventory;
    public ITowerFactory towerFactory;
    private ITowerRegistry _registry = new TowerRegistry();

    private float _yaw;
    
    
    public void Init(GameContext ctx)
    {
        _placement = new BuildPlacementSystem(inventory, towerFactory, _registry);
    }

    public void Shutdown()
    {
        _placement = null;
    }

    public void Tick(float dt)
    {
        if (currentTower == null) return;
        

        if (Input.GetKeyDown(KeyCode.Q)) _yaw -= 90f;
        if (Input.GetKeyDown(KeyCode.E)) _yaw += 90f;

        var q = new BuildPlacementSystem.PlacementQuery
        {
            camera = cam,
            screenPos = Input.mousePosition,
            config = currentTower,
            maxRayDistance = rayDist,
            groundMask = groundMask,
            blockingMask = blockingMask,
            overlapInflation = 0.02f,
            yOffset = 0f,
            ignoreTriggers = true
        };

        var res = _placement.Evaluate(q, _yaw);
        if (res.hasHit)
        {
            
        }
        

        if (res.canPlace && Input.GetMouseButtonDown(0))
        {
            var r = _placement.TryBuildTower(currentTower, res.position, res.rotation);
            Debug.Log($"Build: ok={r.ok} reason={r.reason} id={r.instanceId}");
        }
        
    }
}