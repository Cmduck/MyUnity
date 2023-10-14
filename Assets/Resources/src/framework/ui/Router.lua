---数组描述: [1] url [2] zorder 

--[[
    bundle名称为小写+下划线
--]]

--- ui路由器
local Router = {
    --- 更新界面
    UIUpdater = {url = "hall_ui_updater://ui_updater", zorder = 1, component = "app.ui.CUpdater"},
    --- 登录界面
    UILogin = {url = "hall_ui_login://ui_login", zorder = 1, component = "app.ui.CLogin"},
}

return Router