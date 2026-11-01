using System;
using System.Runtime.CompilerServices;
namespace UnityEngine {
 public class Object {
  public bool destroyed;
  private static bool Null(Object o) => ReferenceEquals(o,null) || o.destroyed;
  public static bool operator ==(Object a,Object b) => Null(a) ? Null(b) : !Null(b) && ReferenceEquals(a,b);
  public static bool operator !=(Object a,Object b) => !(a==b);
  public override bool Equals(object o) => ReferenceEquals(this,o);
  public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
 }
 public class MonoBehaviour : Object { }
 public class Renderer : Object { public bool forceRenderingOff; }
 [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
 [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string s){} }
 public enum RuntimeInitializeLoadType { SubsystemRegistration }
 [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
}
