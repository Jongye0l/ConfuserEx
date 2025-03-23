using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using ConfuserEx_Additions.Jongyeol;
using ConfuserEx.API;
using dnlib.DotNet;

namespace Confuser.Protections {
    public class RenameProtection : Protection {
        public override ProtectionPreset Preset => ProtectionPreset.Minimum;
        public override string Name => "Name Protect";
        public override string Description => "Change All Name";
        public override string Id => "Rename2";
        public override string FullId => "Jongyeol.rename";
        protected override void Initialize(ConfuserContext context) { }

        protected override void PopulatePipeline(ProtectionPipeline pipeline) {
            pipeline.InsertPreStage(PipelineStage.WriteModule, new RenamePhase(this));
        }

        private class RenamePhase : ProtectionPhase {

            public RenamePhase(ConfuserComponent parent) : base(parent) { }

            public override ProtectionTargets Targets => ProtectionTargets.Types;

            public override string Name => "Name Protect Phase";

            protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
                foreach(TypeDef type in parameters.Targets.OfType<TypeDef>().WithProgress(context.Logger)) RenameType(type, RenameTargets.All);
            }

            private static void RenameType(TypeDef type, RenameTargets targets) {
                targets = ParseTargets(targets, type.CustomAttributes);
                if(!type.IsPublic && type.Module.GlobalType != type) {
                    if(targets.HasFlag(RenameTargets.Namespace)) type.Namespace = Rename.RandomName();
                    if(targets.HasFlag(RenameTargets.Type)) type.Name = Rename.RandomName();
                }
                foreach(MethodDef method in type.Methods) {
                    RenameTargets methodTargets = ParseTargets(targets, method.CustomAttributes);
                    if((!method.IsPublic && !method.IsFamilyOrAssembly && !method.IsFamily || !type.IsPublic) && !method.IsConstructor &&
                       !method.IsSpecialName && methodTargets.HasFlag(RenameTargets.Method)) method.Name = Rename.RandomName();
                    foreach(Parameter parameter in method.Parameters) {
                        RenameTargets parameterTargets = ParseTargets(methodTargets, parameter.ParamDef?.CustomAttributes);
                        if(parameterTargets.HasFlag(RenameTargets.Parameter)) parameter.Name = Rename.RandomName();
                    }
                    if(method.HasGenericParameters && methodTargets.HasFlag(RenameTargets.MethodGenericParameter))
                        foreach(GenericParam genericParam in method.GenericParameters) genericParam.Name = Rename.RandomName();
                }
                foreach(FieldDef field in type.Fields) {
                    if(type.IsPublic && (field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly)) continue;
                    RenameTargets fieldTargets = ParseTargets(targets, field.CustomAttributes);
                    if(fieldTargets.HasFlag(RenameTargets.Field)) field.Name = Rename.RandomName();
                }
                foreach(PropertyDef property in type.Properties) {
                    if(property.IsSpecialName || type.IsPublic && (property.IsPublic() || property.IsFamily() || property.IsFamilyOrAssembly())) continue;
                    RenameTargets propertyTargets = ParseTargets(targets, property.CustomAttributes);
                    if(propertyTargets.HasFlag(RenameTargets.Property)) property.Name = Rename.RandomName();
                }
                foreach(EventDef @event in type.Events) {
                    if(type.IsPublic) continue;
                    RenameTargets eventTargets = ParseTargets(targets, @event.CustomAttributes);
                    if(eventTargets.HasFlag(RenameTargets.Event)) @event.Name = Rename.RandomName();
                }
                foreach(TypeDef typeDef in type.GetTypes()) RenameType(typeDef, targets);
                if(type.HasGenericParameters && targets.HasFlag(RenameTargets.TypeGenericParameter))
                    foreach(GenericParam genericParam in type.GenericParameters) genericParam.Name = Rename.RandomName();
            }

            private static RenameTargets ParseTargets(RenameTargets targets, CustomAttributeCollection attributes) {
                if(attributes == null) return targets;
                List<CustomAttribute> removeAttributes = [];
                foreach(CustomAttribute attribute in attributes)
                    if(attribute.TypeFullName == typeof(IncludeRenameAttribute).FullName) {
                        removeAttributes.Add(attribute);
                        targets |= (RenameTargets) attribute.ConstructorArguments[0].Value;
                    } else if(attribute.TypeFullName == typeof(ExcludeRenameAttribute).FullName) {
                        removeAttributes.Add(attribute);
                        targets &= ~(RenameTargets) attribute.ConstructorArguments[0].Value;
                    }
                foreach(CustomAttribute attribute in removeAttributes) attributes.Remove(attribute);
                return targets;
            }
        }
    }
}