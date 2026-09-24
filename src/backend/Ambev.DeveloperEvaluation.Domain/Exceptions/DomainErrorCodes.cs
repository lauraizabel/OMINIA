namespace Ambev.DeveloperEvaluation.Domain.Exceptions;

public static class DomainErrorCodes
{
    public static class User
    {
        public const string EmailAlreadyExists = "User.EmailAlreadyExists";
        public const string NotFound = "User.NotFound";
    }

    public static class Sale
    {
        public const string BranchRequired = "Sale.BranchRequired";
        public const string Cancelled = "Sale.Cancelled";
        public const string CustomerRequired = "Sale.CustomerRequired";
        public const string DateTooFarInFuture = "Sale.DateTooFarInFuture";
        public const string DateRequired = "Sale.DateRequired";
        public const string Deleted = "Sale.Deleted";
        public const string ItemsRequired = "Sale.ItemsRequired";
        public const string InvalidId = "Sale.InvalidId";
        public const string NumberContainsControlCharacter = "Sale.NumberContainsControlCharacter";
        public const string NumberRequired = "Sale.NumberRequired";
        public const string NumberTooLong = "Sale.NumberTooLong";
        public const string NumberAlreadyExists = "Sale.NumberAlreadyExists";
        public const string NotFound = "Sale.NotFound";
        public const string OperationBeforeCreation = "Sale.OperationBeforeCreation";
        public const string TooManyItems = "Sale.TooManyItems";
        public const string VersionConflict = "Sale.VersionConflict";
    }

    public static class SaleItem
    {
        public const string ActiveItemOmitted = "SaleItem.ActiveItemOmitted";
        public const string DuplicateId = "SaleItem.DuplicateId";
        public const string DuplicateProduct = "SaleItem.DuplicateProduct";
        public const string IdNotAllowedOnCreate = "SaleItem.IdNotAllowedOnCreate";
        public const string InvalidId = "SaleItem.InvalidId";
        public const string NotFound = "SaleItem.NotFound";
        public const string NullItem = "SaleItem.Null";
        public const string ProductIsImmutable = "SaleItem.ProductIsImmutable";
        public const string ProductRequired = "SaleItem.ProductRequired";
        public const string QuantityOutOfRange = "SaleItem.QuantityOutOfRange";
        public const string UnitPriceOutOfRange = "SaleItem.UnitPriceOutOfRange";
        public const string UnitPriceScaleExceeded = "SaleItem.UnitPriceScaleExceeded";
    }

    public static class ExternalIdentity
    {
        public const string InvalidId = "ExternalIdentity.InvalidId";
        public const string InvalidName = "ExternalIdentity.InvalidName";
    }
}
