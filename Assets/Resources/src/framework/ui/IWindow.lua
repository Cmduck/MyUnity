local IWindow = class('IWindow')

local tableSuffix = "#"

function IWindow:ctor()
    self.route = nil
    self.ui = {}
    self.gameObject = nil
end

function IWindow:init(gameObject, dict)
    self.gameObject = gameObject
    self:autoBind(dict)
end

function IWindow:reload(...)
    
end

function IWindow:show(...)
    self.gameObject:SetActive(true)
end

function IWindow:hide(...)
    self.gameObject:SetActive(false)
end

function IWindow:close()
    
end

function IWindow:autoBind(dict)
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
        self.ui[k] = v
    end
    dict = nil
end

return IWindow