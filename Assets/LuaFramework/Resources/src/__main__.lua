local cjson = require("cjson");

__G__TRACKBACK__ = function(msg)
    local msg = debug.traceback(msg, 3)
    print(msg)
    return msg
end

local ENABLE_UPDATE =  true

local BUILD_VER = "BUILD_VER"
local SEARCH_PATHS = "SEARCH_PATHS"
local MODULE_VERSION = "MODULE_VERSION"
-- 判断是否整包解压过
local ZIP_STATUS = "ZIP_STATUS"


print("run __main__");
print("_ENV:", _ENV);

local function decode(text)
    local status, result = pcall(cjson.decode, text)
    if status then return result end

    print("decode() - ERROR:", tostring(result))
end


local function main()
    local playerPrefs = UnityEngine.PlayerPrefs;
    local application = UnityEngine.Application;

    -- table: {time:xxx, modules:['hall', 'public']} modules用于指示包内的模块 用于启动解压
    local appmeta = {}
    if ENABLE_UPDATE then
        local content = UnityTools.LoadJsonFromResources("app")
        print("[__main__] content:", content)
        appmeta = decode(content)

        -- 构建版本(对应整包版本)
        local buildVer = playerPrefs.GetFloat(BUILD_VER, 0)
        local mode = 0;
        if buildVer == 0 or buildVer ~= appmeta.time then
            -- 首次安装或整包更新
            print('[__main__] first install or change package')
            -- 缓存时间
            playerPrefs.SetFloat(BUILD_VER, appmeta.time)
            -- 清空路径
            playerPrefs.DeleteKey(SEARCH_PATHS)
            -- 清空版本
            playerPrefs.DeleteKey(MODULE_VERSION)
            -- 清空解压
            playerPrefs.DeleteKey(ZIP_STATUS)

            --确保加载包内的资源
            FileUtils.Instance:addSearchPath(application.streamingAssetsPath  .. '/data/')
        else
            -- 确保加载上一次热更的资源
            print('[__main__] hot update')
            -- 热更
            local searchPaths = playerPrefs.GetString(SEARCH_PATHS, "")

            print('searchPaths:', searchPaths);

            if searchPaths ~= nil and searchPaths ~= "" then
                mode = 1
                local paths = decode(searchPaths)
                if paths ~= nil and #paths > 0 then
                    FileUtils.Instance:setSearchPaths(paths)
                end
            end
        end

        print("buildVer = ", buildVer)


        -- CCLuaLoadChunksFromZIP(newUpdatePackage)
        require('launcher.launcher').new(mode, appmeta):run(true);
    else
        require('app.MyApp').run(appmeta)
    end
end

local status, msg = xpcall(main, __G__TRACKBACK__)
if not status then
    print(msg)
end