This week's topic is about the standard implementations for the different ways to compare instances of a type. There are 7 interfaces in .NET that are used for this: IEqualityComparer, IEqualityComparer<T>, IComparer, IComparer<T>, Comparable, IComparable<T>, and IEquatable<T>. Notice the missing, non-generic IEquatable variant. This is because object already has those methods that can be overridden by any derived type. Let's know look at the standard implementations:

1. `IEqualityComparer`
  
```
public sealed class FooComparer : IEqualityComparer
{
  public bool Equals(object x, object y)
  {
    if (object.ReferenceEquals(x, y))
    {
      return true;
    }

    if (x == null || y == null)
    {
      return false;
    }
  
    // implementation specific logic should go here
    ...
  
  TODO equalitycomparer.default
  }
  
  public int GetHashCode(object obj)
  {
    if (obj == null)
    {
      throw new ArgumentNullException(nameof(obj));
    }
  
    // implementation specific logic should go here
    ...
  
  TODO equalitycomparer.default
  }
}
```

There are a few notes to observe about this implementation. First, the use of `object.ReferenceEquals`. Normally, we could just write `if (x == y)`. However, because `x` is of type `object`, we do not know if `object.Equals` has been overloaded for the underlying type of `x`. This might mean that `x == y` will perform many more operations than are necessary if `x` and `y` are the same instance. `object.ReferenceEquals` performs *exactly* this check. Second, note that we do not need to be concerned about `null` once the first two `if` statements are evaluated. If `x` and `y` are both `null`, `object.ReferenceEquals` will cause use to return `true`. This means that the second `if` statement does not need to check if they are both `null`, only if either one of them is `null`. If either is `null`, we know they must be different because we already know they aren't *both* `null`. Third, there is a possible exception that can be thrown in `GetHashCode`. Although it can be tempting to use a default value for the case where `obj` is `null` (often `0` is used), it is best to follow the contract specified by the interface and let the caller dictate whether to handle the `null` case or not. Using a default value ultimately skews the distribution of hash codes, which is not always ideal. 
  
2. `IEqualityComparer<T>`

```
public sealed class FooComparer : IEqualityComparer<Foo>
{
  public bool Equals(Foo x, Foo y)
  {
    if (object.ReferenceEquals(x, y))
    {
      return true;
    }

    // omit this check if Foo is a value type
    if (x == null || y == null)
    {
      return false;
    }
  
    // implementation specific logic should go here
    ...
  
  TODO equalitycomparer<T>.default
  }
  
  public int GetHashCode(Foo obj)
  {
    // omit this check if Foo is a value type
    if (obj == null)
    {
      throw new ArgumentNullException(nameof(obj));
    }
  
    // implementation specific logic should go here
    ...
  
  TODO equalitycomparer<T>.default
  }
}
```
  
The implementation of the generic interface is extremely close to the non-generic implementation. Notice that `object.ReferenceEquals` is still used. This is because, while we might know the `Equals` implementation for `Foo` when the comparer is written, the implementer of `Foo` *could* change that overload in the future, and that would potentially break our logic. It is better in this case to be explicit about our intention (we are trying to check reference equality) and avoid being broken in the future. We are also able to remove the `null` checks for the generic implementation if `Foo` is a value type. Those checks might be necessary if `Foo` is later changed to a reference type, but such a change would be a breaking change and we are not responsible for handling all possible future breaking changes. 
  
3. `IComparer`
  
```
public sealed class FooComparer : IComparer
{
  
}
```