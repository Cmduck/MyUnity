local cjson = require("cjson");

local control = {}
local UnityWebRequest = UnityEngine.Networking.UnityWebRequest

-- 强更版本
control.update = 0

-- 版号
control.banhao = 0
-- 屏蔽字
control.wfilter = 0
-- 开关
control.onoff = 0
-- h5
control.h5_ser_err = 0
control.h5_ver = 0

-- 用户协议
control.user_agreement = 0
-- 隐私协议
control.private_agreement = 0
control.cs = 0
control.minigame_share = 0
control.wx_mini_shenhe_ver = 0
control.shenhe_onoff = 0

-- 大厅版本
control.hall = 0
-- 长城开关 0 关闭长城 1 打开长城1 2 打开长城2
control.greatwall = 0

---comment
---@param cb function 回调函数
function control.init(cb)
    local url = GConf.cdnEnv() .. "/apk_201/config.json?t=" .. math.floor(os.time()/60)

    print('总控地址:', url)
    -- local uwr = UnityWebRequest.Get(url);
    -- coroutine.www(uwr);
    -- local content = tolua.tolstring(uwr.downloadHandler.text);

	local www = UnityWebRequest.Get(url)
    www:SendWebRequest();
	coroutine.www(www);
    local tbl = ToTable(www.downloadHandler.text)

    if tbl == nil then cb(true) end


    for k, v in pairs(tbl) do
        control[k] = v
    end

    cb(false)
end

return control