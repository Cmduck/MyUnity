
local conf = {}
local self = conf

conf.cdnRoot = "https://g2022jjmj-cdn.laiyouxi.com/jjmj"
conf.cdnProj = "http://192.168.20.222:5555/apk_jjmj"

function conf.cdnEnv()
    if LuaFramework.AppConst.env == 2 then
        return self.cdnProj .. "/online"
    end

    return self.cdnProj .. "/test"
end

return conf