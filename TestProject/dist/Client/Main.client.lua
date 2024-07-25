local CS = require(game:GetService("ReplicatedStorage").rbxcs_include.RuntimeLib)

-- using ComponentRunner;
require(game:GetService("ReplicatedStorage")["C#"]["Components"])

-- using LavaComponent;
require(game:GetService("Players").LocalPlayer["PlayerScripts"]["C#"]["Components"]["Lava"])

CS.namespace("TestGame", function(namespace)
    namespace:namespace("Client", function(namespace)
        namespace:class("Game", function(namespace)
            local class = CS.classDef("Game", namespace)
            
            function class.Main()
                CS.getAssemblyType("Components").ComponentRunner.AttachTag("Lava", function(instance)
                    local lava = namespace["$getMember"](namespace, "LavaComponent").new(instance)
                    print("[TestProject/Client/Main.client.cs:13:17]:", lava)
                    return lava
                end)
            end
            
            if namespace == nil then
                class.Main()
            else
                namespace["$onLoaded"](namespace, class.Main)
            end
            return class
        end)
    end)
end)