using System;
using Mafi;
using Mafi.Core.Economy;
using Mafi.Core.Entities.Static;
using Mafi.Core.Ports.Io;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;

namespace CoI.AutoHelpers.Compatibility;

/// <summary>
/// Adapts transient, single-product logistics buffers to the 4.3 interface.
/// Derived buffers remain responsible for moving and accounting for their product.
/// </summary>
public abstract class PlainProductBufferAdapter :
// PRE_4_3_COMPAT: legacy branches/API guards only; remove after Update 5 or when pre-4.3 support ends.
#if COI_UPDATE_4_3
    IPlainProductBuffer
#else
    IProductBuffer
#endif
{
    public abstract ProductProto Product { get; }
    public abstract Quantity Capacity { get; }
    public abstract Quantity UsableCapacity { get; }
    public abstract Quantity Quantity { get; }
    public abstract Quantity StoreAsMuchAs(Quantity quantity);
    public abstract Quantity RemoveAsMuchAs(Quantity quantity);

#if COI_UPDATE_4_3
    public IPlainProductBuffer AsPlainBuffer() => this;
    public virtual bool CanStore(ProductProto product, Quantity quantity) =>
        product == Product && quantity <= UsableCapacity;
    public bool CanRemove(ProductProto product, Quantity quantity) =>
        product == Product && quantity <= Quantity;
    Quantity IPlainProductBuffer.StoreAsMuchAs(Quantity quantity, bool doNotReport) => StoreAsMuchAs(quantity);
    Quantity IPlainProductBuffer.RemoveAsMuchAs(Quantity quantity, bool doNotReport) => RemoveAsMuchAs(quantity);

    public void StoreAllIgnoreCapacity(Quantity quantity)
    {
        // These adapters never own storage; silently dropping an overflow would lose products.
        if (quantity.IsPositive)
            throw new InvalidOperationException("A transient logistics buffer cannot accept overflow products.");
    }

    public Quantity RemoveAll() => RemoveAsMuchAs(Quantity);
    public void ClearAndStoreProduct(IAssetTransactionManager atm) =>
        atm.StoreProduct(Product.WithQuantity(RemoveAll()), null);
    public Quantity SendAsMuchAsToPort(IoPortData port)
    {
        Quantity available = Quantity;
        Quantity accepted = available - port.SendAsMuchAs(Product.WithQuantity(available));
        return RemoveAsMuchAs(accepted);
    }
    public Quantity RemoveAndDestroyAsMuchAs(IProductsManager productsManager, Quantity maximum,
        DestroyReason reason, Option<Proto> destroyer = default)
    {
        Quantity removed = RemoveAsMuchAs(maximum);
        if (removed.IsPositive) productsManager.ProductDestroyed(destroyer, Product, removed, reason);
        return removed;
    }
    public Quantity TransferTo(IProductBuffer target, Quantity quantity, bool doNotReport, bool ignoreTargetCapacity = false)
    {
        if (target.Product != Product || ReferenceEquals(this, target)) return Quantity.Zero;
        Quantity available = quantity.Min(Quantity);
        if (ignoreTargetCapacity)
        {
            target.AsPlainBuffer().StoreAllIgnoreCapacity(available);
            return RemoveAsMuchAs(available);
        }
        Quantity accepted = available - target.AsPlainBuffer().StoreAsMuchAs(available, doNotReport);
        return RemoveAsMuchAs(accepted);
    }
#endif
}
