local IWindow = require('framework.ui.IWindow')
local CUpdater = class("CUpdater", IWindow)

-- 判断是否整包解压过
local ZIP_STATUS = "ZIP_STATUS"


---提取bundle
---@param modules string[]
---@param writablePath string
local function _coExtractBundle(modules, writablePath)
    local File = System.IO.File
    local streamingDataPath = Application.streamingAssetsPath .. "/data"; 
    print("streamingDataPath:", streamingDataPath);

    for i = 1, #modules, 1 do
        local content;
        local _tbl = {streamingDataPath, "/", modules[i], "_manifest.json"};
        local url = table.concat(_tbl);
        print('url:', url);

        if device.platform ~= "android" then
            content = File.ReadAllText(url);
        else
            local uwr = UnityWebRequest.Get(url);
            coroutine.www(uwr);
            content = tolua.tolstring(uwr.downloadHandler.text);
        end

        print('content:', content);

        local manifest = json.decode(content);
        local assets = manifest.assets;

        for k, v in pairs(assets) do
            print('bundle:', k);
        end
    end

    -- print('百度');
	-- local www = UnityWebRequest.Get("http://www.baidu.com")
    -- www:SendWebRequest();
	-- coroutine.www(www);
	-- local s = www.downloadHandler.text;
	-- print('百度ret ' .. s:sub(1, 128));
    -- print('Coroutine ended');
end


function CUpdater:ctor(...)
    self.super.ctor(self, ...)
end

function CUpdater:reload(appmeta)
    print('[CUpdater] reload() - ')

    local zipStatus = PlayerPrefs.GetInt(ZIP_STATUS, 0);

    if zipStatus == 0 then
        -- 没有压缩 开始压缩
        self:runZip(appmeta)
    else
        self:runUpdate(appmeta)
    end
end

function CUpdater:runZip(appmeta)
    self.ui['desc'].text = "正在解压资源...";
    self.ui['slider'].value = 0;

    -- {"hall", "public"}
    local modules = appmeta.modules;
    local writablePath = Application.persistentDataPath;

    Launcher.Instance:WrapExtract(modules, writablePath, handler(self, self.launchProgress), handler(self, self.launchComplete));
end

function CUpdater:runUpdate(appmeta)
    self.ui['desc'].text = "正在检测更新...";
    self.ui['slider'].value = 0;
end

---启动进度
---@param finished number 完成数目
---@param total number 总共数目
function CUpdater:launchProgress(finished, total)
    print('finished:' .. finished .. " total:" .. total);
    self.ui['slider'].value = finished/total;
end

---启动完成
---@param err number 0 正常 1 错误
function CUpdater:launchComplete(err)
    print('启动完成 error:' .. err);
end

return CUpdater