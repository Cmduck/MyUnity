GConf = require("launcher.conf")

local UpdaterPanel = {}
local this = UpdaterPanel

local UpdateHelper = require("launcher.updaterHelper")

local luaHelper = LuaFramework.LuaHelper
local panelMgr = luaHelper.GetPanelManager()
local tableSuffix = "#"
local route = {url = "hall_ui_updater://ui_updater", zorder = 1, component = "launcher.UpdaterPanel"};

UpdaterPanel.ui = {}
UpdaterPanel.go = nil
UpdaterPanel.meta = nil
UpdaterPanel.updaterHelper = UpdateHelper.new("hall", UpdaterPanel) 

local application = UnityEngine.Application
local file = System.IO.File
local playerPrefs = UnityEngine.PlayerPrefs;

--可写目录
local writablePath = application.persistentDataPath .. "/remote-hall/src"

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
        UpdaterPanel.ui[k] = v
    end
    dict = nil
end

function UpdaterPanel.run(meta)
    this.meta = meta

    local url       = route.url
    local zorder    = route.zorder

    panelMgr:LoadPanel(url, zorder, function (gameObject, dict)
        if (dict ~= nil) then
            this.go = gameObject
            autoBind(dict)
            this.check()
        end
    end, true)
end

function UpdaterPanel.check()
    -- ZIP_STATUS
    local status = playerPrefs.GetInt(ZIP_STATUS, 0);

    print("ZIP_STATUS:", status)

    if status == 0 then
        -- 解压
        print('启动解压')
        this.unzip()
    else
        -- 热更
        print('检测更新')
        this.checkUpdate()
    end
end

function UpdaterPanel.unzip()
    this.ui['desc'].text = "正在解压资源...";
    this.ui['slider'].value = 0;

    -- {"hall", "public"}
    local modules = this.meta.modules;
    local writablePath = application.persistentDataPath;

    Launcher.Instance:WrapExtract(modules, writablePath, this.launchProgress, this.launchComplete);
end

---启动进度
---@param finished number 完成数目
---@param total number 总共数目
function UpdaterPanel.launchProgress(finished, total)
    print('finished:' .. finished .. " total:" .. total);
    this.ui['slider'].value = finished/total;
end

---启动完成
---@param err number 0 正常 1 错误
function UpdaterPanel.launchComplete(err)
    print('启动完成 error:' .. err);

    if err == 1 then
        error("解压错误")
        return
    end

    playerPrefs.SetInt(ZIP_STATUS, 1)

    this.checkUpdate()
end

function UpdaterPanel.checkUpdate()
    this.ui['desc'].text = "正在检测更新...";
    this.ui['slider'].value = 0;

    this.updaterHelper:createUpdater()
    this.updaterHelper:start()
end

---获取总控失败
---@param msg string 错误消息
function UpdaterPanel.onGetConfigFailed(msg)
    
end

---服务器维护
---@param errId number 错误id
function UpdaterPanel.onServerMaintain(errId)
    local errUrl = GConf.cdnProj .. "/h5/zh-CN/settings/ser-error-" .. errId .. ".html"

    -- 显示webview节点
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
---@param module string 热更模块
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
---@param restart boolean 是否重启
function UpdaterPanel.onUpdateOver(restart)
    print('[UpdaterPanel] onUpdateOver: ', restart)
end

return UpdaterPanel