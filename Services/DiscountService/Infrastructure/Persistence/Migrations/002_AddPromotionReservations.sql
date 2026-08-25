ALTER TABLE Coupons
    ADD COLUMN IF NOT EXISTS MaxRedemptions integer NULL,
    ADD COLUMN IF NOT EXISTS RedemptionCount integer NOT NULL DEFAULT 0;

ALTER TABLE Coupons
    ADD CONSTRAINT CK_Coupons_RedemptionCount_NonNegative CHECK (RedemptionCount >= 0);

CREATE TABLE IF NOT EXISTS PromotionReservations (
    Id uuid PRIMARY KEY,
    CouponCode text NOT NULL REFERENCES Coupons(Code),
    OrderId uuid NOT NULL,
    CustomerId uuid NOT NULL,
    OrderAmount numeric(18, 2) NOT NULL,
    DiscountAmount numeric(18, 2) NOT NULL,
    FinalAmount numeric(18, 2) NOT NULL,
    Status text NOT NULL,
    ExpiresAtUtc timestamptz NOT NULL,
    CreatedAtUtc timestamptz NOT NULL,
    UpdatedAtUtc timestamptz NOT NULL,
    ReleaseReason text NULL,
    CONSTRAINT UQ_PromotionReservations_Coupon_Order UNIQUE (CouponCode, OrderId),
    CONSTRAINT CK_PromotionReservations_Amounts CHECK (OrderAmount > 0 AND DiscountAmount >= 0 AND FinalAmount >= 0)
);

CREATE INDEX IF NOT EXISTS IX_PromotionReservations_ActiveExpiry
    ON PromotionReservations (CouponCode, ExpiresAtUtc)
    WHERE Status = 'Reserved';
