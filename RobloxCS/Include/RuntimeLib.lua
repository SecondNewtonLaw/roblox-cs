local CS = {}
local assemblyGlobal = {}

local fempty = function() end

local split = string.split
if split == nil then
	split = function(inputString, separator)
		if sep == nil then
			sep = "%s"
		end
		local t = {}
		for str in string.gmatch(inputString, "([^"..sep.."]+)") do
			table.insert(t, str)
		end
		return t
	end
end

local function chainIndex(location, ...)
    local names = {...}
    return function(t, k)
        for _, name in pairs(names) do
			local tbl = location[name]
            local v = tbl[k]
            if v ~= nil then
                return v
            end
        end
    end
end

export type Class = table;
export type Namespace = {
	name: string;
	parent: Namespace?;
	members: { Namespace | Class };
	class: (self: Namespace, name: string, create: (self: Namespace) -> Class) -> nil;
}

local CSNamespace = {} do
	function CSNamespace.new(name, parent)
		local self = {}
		self.name = name
		self.parent = parent
		self.members = {}
		self["$loadCallbacks"] = {}
		if self.parent ~= nil then
			self = setmetatable(self, self.parent)
		end
		return setmetatable(self, CSNamespace)
	end

	function CSNamespace:__index(index)
		return self.members[index] or CSNamespace[index]
	end

	function CSNamespace:__tostring(index)
		return self.name
	end

	CSNamespace["$getMember"] = function(self, name)
		return self.members[name]
	end

	CSNamespace["$onLoaded"] = function(self, callback)
		table.insert(self["$loadCallbacks"], callback)
	end

	function CSNamespace:class(name, create)
		CS.class(name, create, self)
	end

	function CSNamespace:namespace(name, registerMembers)
		CS.namespace(name, registerMembers, self.members, self)
	end
end

function CS.classInstance(class, mt, namespace)
	local instance = {}

	local function getSuperclass()
		if class.__superclass == nil then return end
		if class.__superclass:match(".") == nil then
			return assemblyGlobal[class.__superclass]
		end

		local pieces = class.__superclass:split(".");
		local result
		for _, piece in pairs(pieces) do
			result = (result or assemblyGlobal)[piece]
		end
		return result
	end

	function mt.__tostring()
		return class.__name
	end

	instance["$base"] = function(...)
		if instance["$superclass"] ~= nil then return end
		local Superclass = getSuperclass()
		local superclassInstance = Superclass.new(...)
		instance["$superclass"] = superclassInstance
		mt.__index = superclassInstance
	end

	return setmetatable(instance, mt)
end

function CS.classDef(name, namespace, superclass, ...)
	local mixins = {...}
	local mt = {}
	mt.__index = chainIndex(if namespace ~= nil then namespace else assemblyGlobal, ...)

	function mt.__tostring()
		return name
	end

	local class = {}
	class.__name = name
	class.__superclass = superclass
	return setmetatable(class, mt)
end

function CS.class(name, create, namespace)
	local location = if namespace ~= nil then namespace.members else assemblyGlobal
	local class = create(namespace)
	location[name] = class
end

function CS.namespace(name, registerMembers, location, parent)
	if location == nil then
		location = assemblyGlobal
	end

	local namespaceDefinition = location[name] or CSNamespace.new(name, parent)
	registerMembers(namespaceDefinition)
	for _, callback in pairs(namespaceDefinition["$loadCallbacks"]) do
		callback()
	end

	location[name] = namespaceDefinition
	return namespaceDefinition
end

function CS.getAssemblyType(name)
	local env
	if getfenv == nil then
		env = _ENV
	else
		env = getfenv()
	end
	return assemblyGlobal[name] or env[name]
end

return CS