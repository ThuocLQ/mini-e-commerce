export type CurrentUser = {
  userId: string;
  userName: string;
  role: string;
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
};