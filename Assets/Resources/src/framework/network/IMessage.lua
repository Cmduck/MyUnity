--[[    通用json消息    
]]
function CreateJsonSend(funcid, t)
    local message = {}

    function message.GetTM()
        return 1
    end

    function message.GetFuncID()
        return funcid
    end

    function message.ToString()
        if t ~= nil then
            return json.encode(t)
        end

        return ""
    end

    function message.Encode()
        local bytebuffer = ByteBuffer.Get()
        if t ~= nil then
            local str = json.encode(t)
            bytebuffer:WriteString(str)
        end

        return bytebuffer
    end
	message.t = t
    return message
end


function CreateHeartbeatSend(hbTime)
    local _tm = 2
    local _funcId = 6
    -- 默认15秒
    local _hbTime = hbTime or 15
    local message = {}

    function message.GetTM()
        return _tm
    end

    function message.GetFuncID()
        return _funcId
    end

    function message.Encode()
        local bytebuffer = ByteBuffer.Get()
        bytebuffer:WriteInt32(_hbTime)

        return bytebuffer
    end
	message.t = _hbTime
    return message
end

function CreateHeartbeatRecv()
    local _tm = 2
    local _funcId = 6

    local message = {}

    function message.Decode(bytebuffer)
        local serverTime = bytebuffer:ReadInt32()

        local ret = {
            ['__retErr'] = 0,
            ['serverTime'] = serverTime
        }

        return ret
    end

    return message
end

function CreateBuffSend(funcid, t)
    local message = {}

    function message.GetTM()
        return 2
    end

    function message.GetFuncID()
        return funcid
    end

	function message.Encode()
        return t
    end

	message.t = t
    return message
end
