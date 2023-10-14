local UIManager = {};

function UIManager.init()
    
end

---打开界面
---@param route table 路由配置
function UIManager.open(route, cb)
    --- 加载预制体 绑定控制器
    local url       = route.url
    local zorder    = route.zorder

    local comp = require(route.component).new()
    comp.route = route

    panelMgr:LoadPanel(url, zorder, function (gameObject, dict)
        if (dict ~= nil) then
            comp:init(gameObject, dict)
            
            if cb ~= nil then
                cb(comp)
            end
        end
    end, true)
    
    --TODO local tbl = panelMgr:LoadPanel(url, zorder);
end


function UIManager.close(route)
    
end

return UIManager