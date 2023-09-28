local IWindow = require('framework.ui.IWindow')
local CLogin = class("CLogin", IWindow)


function CLogin:ctor(...)
    self.super.ctor(self, ...)
end

function CLogin:reload()
    print('[CLogin] reload() - ')
    --self.ui['desc'].text = "测试"
    --self.ui['slider'].value = 0.7
end

return CLogin