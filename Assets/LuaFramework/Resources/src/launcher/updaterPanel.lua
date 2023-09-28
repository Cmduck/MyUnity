local UpdaterPanel = {}

local luaHelper = LuaFramework.LuaHelper
local panelMgr = luaHelper.GetPanelManager()
local tableSuffix = "#"
local route = {url = "hall_ui_updater://ui_updater", zorder = 1, component = "launcher.UpdaterPanel"};

local ui = nil
local go = nil

local application = UnityEngine.Application
local file = System.IO.File

--可写目录
local writablePath = application.persistentDataPath .. "/remote-hall/src"

local launcherPath = writablePath .. "/launcher.zip"

local function autoBind(dict)
    local iter = dict:GetEnumerator()
    while iter:MoveNext() do
        if (iter.Current == nil or iter.Current.Key == nil) then
            break
        end
        local k = tostring(iter.Current.Key)
        local v = iter.Current.Value

        local pos = string.find(k, tableSuffix)
        if (pos ~= nil) then
            --log("Key:"..k.."=".."Value:"..table_tostring(tmpTable))
            k = string.gsub(k, tableSuffix, "")
            --此时的v是一个c#字典
            local tableIter = v:GetEnumerator()
            local tmpTable = {}
            while tableIter and tableIter:MoveNext() do
                local curr = tableIter.Current
                if (curr == nil or curr.Key == nil) then
                    break
                end
                tmpTable[tonumber(curr.Key)] = curr.Value
            end
            v = tmpTable
        else
            --log("Key:"..k.."=".."Value:"..v.name.."("..v:GetType():ToString()..")")
        end
        ui[k] = v
    end
    dict = nil
end

function UpdaterPanel.run(checkNewUpdater)
    local url       = route.url
    local zorder    = route.zorder

    panelMgr:LoadPanel(url, zorder, function (gameObject, dict)
        if (dict ~= nil) then
            go = gameObject;
            autoBind(dict);
            
        end
    end, true)
end

-- Is there a launcher.zip package in ures directory?
-- If it is true, return its abstract path.
function UpdaterPanel.hasNewUpdatePackage()
	if file.Exists(launcherPath) then
		return launcherPath
	end
	return nil
end

---获取总控失败
---@param msg string 错误消息
function UpdaterPanel.onGetConfigFailed(msg)
    
end

---服务器维护
---@param url string 公告地址
function UpdaterPanel.onServerMaintain(url)
    
end

---强制更新
---@param updateTab table {url: string, md5: string, title: string, des: string[], ver?: number, type?: string}  type 1: 打开网页 other: 直接下载
function UpdaterPanel.onForceUpdate(updateTab)
    
end

---预备更新
---@param module string 热更模块
---@param hotVer number 热更版本
---@param netType number 网络类型
function UpdaterPanel.onPrepareUpdate(module, hotVer, netType)
    
end

---开始更新
---@param module string 热更模块
---@param updateTab table {url: string, md5: string, title: string, des: string[], ver?: number, type?: string}  type 1: 打开网页 other: 直接下载
function UpdaterPanel.onStartUpdate(module, updateTab)
    
end

---更新进度
---@param module 热更模块
---@param progressTab table {percent: number 下载比例, downloadedFiles?: number 已下文件, downloadedBytes: number 已下字节, totalFiles?: number 全部文件, totalBytes: number 全部字节}
function UpdaterPanel.onUpdateProgress(module, progressTab)
    
end

---更新失败
function UpdaterPanel.onUpdateFailed()
    
end

---更新成功
function UpdaterPanel.onUpdateSuccess()
    
end

---更新结束
function UpdaterPanel.onUpdateOver(restart)
    
end

return UpdaterPanel