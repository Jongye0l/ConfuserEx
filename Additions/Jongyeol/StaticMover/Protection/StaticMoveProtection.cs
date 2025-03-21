using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using ConfuserEx.API;
using dnlib.DotNet;

namespace Confuser.Protections {
    public class StaticMoveProtection : Protection {
        public override ProtectionPreset Preset => ProtectionPreset.Minimum;
        public override string Name => "Static Mover";
        public override string Description => "All Static Code Move Global Type";
        public override string Id => "static mover";
        public override string FullId => "Confuser.Jongyeol.staticMover";
        protected override void Initialize(ConfuserContext context) { }

        protected override void PopulatePipeline(ProtectionPipeline pipeline) {
            pipeline.InsertPreStage(PipelineStage.WriteModule, new ShufflePhase(this));
        }

        private class ShufflePhase : ProtectionPhase {

            public ShufflePhase(ConfuserComponent parent) : base(parent) { }

            public override ProtectionTargets Targets => ProtectionTargets.Modules;

            public override string Name => "Static mover";

            protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
                foreach(ModuleDefMD module in parameters.Targets.OfType<ModuleDefMD>().WithProgress(context.Logger)) {
                    TypeDef globalType = module.GlobalType;
                    foreach(TypeDef type in module.Types) MoveMembers(type, globalType, StaticMoveTargets.All);
                }
            }

            private static void MoveMembers(TypeDef type, TypeDef globalType, StaticMoveTargets targets) {
                targets = ParseTargets(targets, type.CustomAttributes);
                if(type != globalType) {
                    MethodDef[] methods = type.Methods.ToArray();
                    foreach(MethodDef method in methods) {
                        if(!method.HasBody || !method.IsStatic || method.IsSpecialName || method.IsPublic && type.IsPublic) continue;
                        type.Methods.Remove(method);
                        globalType.Methods.Add(method);
                        method.DeclaringType = globalType;
                    }
                    FieldDef[] fields = type.Fields.ToArray();
                    foreach(FieldDef field in fields) {
                        if(field.IsStatic || field.IsSpecialName || field.IsPublic && type.IsPublic) continue;
                        type.Fields.Remove(field);
                        globalType.Fields.Add(field);
                        field.DeclaringType = globalType;
                    }
                    PropertyDef[] properties = type.Properties.ToArray();
                    foreach(PropertyDef property in properties) {
                        if(property.IsSpecialName || property.IsPublic() && type.IsPublic) continue;
                        type.Properties.Remove(property);
                        globalType.Properties.Add(property);
                        property.DeclaringType = globalType;
                    }
                    EventDef[] events = type.Events.ToArray();
                    foreach(EventDef @event in events) {
                        if(@event.IsSpecialName || type.IsPublic) continue;
                        type.Events.Remove(@event);
                        globalType.Events.Add(@event);
                        @event.DeclaringType = globalType;
                    }
                }
                foreach(TypeDef typeDef in type.GetTypes()) MoveMembers(typeDef, globalType, targets);
            }

            private static StaticMoveTargets ParseTargets(StaticMoveTargets targets, CustomAttributeCollection attributes) {
                if(attributes == null) return targets;
                List<CustomAttribute> removeAttributes = [];
                foreach(CustomAttribute attribute in attributes)
                    if(attribute.TypeFullName == typeof(IncludeStaticMoveAttribute).FullName) {
                        removeAttributes.Add(attribute);
                        targets |= (StaticMoveTargets) attribute.ConstructorArguments[0].Value;
                    } else if(attribute.TypeFullName == typeof(ExcludeStaticMoveAttribute).FullName) {
                        removeAttributes.Add(attribute);
                        targets &= ~(StaticMoveTargets) attribute.ConstructorArguments[0].Value;
                    }
                foreach(CustomAttribute attribute in removeAttributes) attributes.Remove(attribute);
                return targets;
            }
        }
    }
}