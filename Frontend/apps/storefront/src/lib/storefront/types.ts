export type CurrentUser = {
  userId: string;
  userName: string;
  role: string;
  isEmailVerified: boolean;
  receiveOrderUpdates: boolean;
};

export type BasketItem = {
  productId: string;
  productName: string | null;
  quantity: number;
  price: number;
};

export type Basket = {
  userId: string;
  basketId: string;
  items: BasketItem[];
  totalPrice: number;
  version: number;
};

export type CustomerAddress = {
  id: string;
  label: string;
  recipientName: string;
  line1: string;
  line2: string | null;
  city: string;
  countryCode: string;
  postalCode: string | null;
  isDefault: boolean;
  isArchived: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type AddressInput = {
  label: string;
  recipientName: string;
  line1: string;
  line2: string | null;
  city: string;
  countryCode: string;
  postalCode: string | null;
  makeDefault: boolean;
};

export type OrderSummary = {
  id: string;
  createdAtUtc: string;
  status: string;
  totalAmount: number;
  currency: string;
  items: Array<{
    id: string;
    productId: string;
    productName: string;
    unitPrice: number;
    quantity: number;
    totalPrice: number;
  }>;
  shippingAddress: {
    addressId: string;
    label: string;
    recipientName: string;
    line1: string;
    line2: string | null;
    city: string;
    countryCode: string;
    postalCode: string | null;
  } | null;
};

export type PaymentSummary = {
  id: string;
  orderId: string;
  customerId: string;
  amount: number;
  currency: string;
  status: string;
  failureReason: string | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
  provider: string | null;
  providerCheckoutUrl: string | null;
  paymentActionExpiresAtUtc: string | null;
};
export type CheckoutQuote = {
  basketId: string;
  basketVersion: number;
  quoteToken: string | null;
  canCheckout: boolean;
  issues: Array<{
    code: string;
    message: string;
    productId: string | null;
  }>;
  evaluatedAtUtc: string;
  expiresAtUtc: string;
  finalRevalidationRequired: boolean;
  currency: string;
  subtotalAmount: number;
  discountAmount: number;
  totalAmount: number;
  coupon: {
    couponCode: string | null;
    isValid: boolean;
    discountAmount: number;
    finalAmount: number;
    message: string;
  };
  items: Array<{
    productId: string | null;
    basketProductName: string | null;
    productName: string | null;
    quantity: number;
    basketUnitPrice: number;
    currentUnitPrice: number | null;
    basketLineTotal: number;
    currentLineTotal: number | null;
    priceChanged: boolean;
    availability: boolean;
  }>;
};