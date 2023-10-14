require("app.declare")
require("framework.init")

local MyApp = {}
local this = MyApp

function MyApp.run(appmeta)
    print('[MyApp] run() - ')
    math.randomseed(os.time())
    this.bindSystemEvent()
    this.import()
    this.init(appmeta)

    local fileUtils = FileUtils.Instance;

    local searchPaths = fileUtils:getSearchPaths();
    local count = searchPaths.Count;
    searchPaths:ForEach(function(v) print('搜索路径: '..v) end)

end

---初始化
---@param appmeta table {time:12, modules:{"hall", "public"}}
function MyApp.init(appmeta)
    -- 设置帧率
    this.setFramerate(30)
    -- 获取剪切板 TODO

    -- 壳子初始化 TODO

    -- 启动初始界面(首次安装时 解压文件 非首次安装时 设置缓存搜索目录 检测更新)
    GUI.open(Router.UIUpdater, function (comp)
        comp:reload(appmeta)
    end);
end

function MyApp.import()
    AppConfig = require('app.AppConfig')
end

function MyApp.setFramerate(num)
    Application.targetFrameRate = num
end

function MyApp.bindSystemEvent()
    gameMgr.LuaFunc_Mono_OnApplicationPause = this.onAppPause
    gameMgr.LuaFunc_Mono_OnApplicationQuit = this.onAppQuit

    -- gameMgr.LuaFunc_Mono_OnApplicationPause = function(_pause)
	-- 	Event.Brocast(Brocast_Event.SYS_APPLICATIONPAUSE, _pause)
    -- end

    -- gameMgr.LuaFunc_Mono_OnApplicationQuit = function()
	-- 	Event.Brocast(Brocast_Event.SYS_APPLICATIONQUIT)
    -- end
end

--- 应用暂停
function MyApp.onAppPause(bPause)
    print('[MyApp] onAppPause() - ', bPause)
end

--- 应用退出
function MyApp.onAppQuit()
    print('[MyApp] onAppQuit() - ', bPause)
end

--- 进入后台
function MyApp.onEnterBackground()
    
end

--- 进入前台
function MyApp.onEnterForeground()
    
end

return MyApp