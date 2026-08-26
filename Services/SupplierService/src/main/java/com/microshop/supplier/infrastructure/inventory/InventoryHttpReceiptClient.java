package com.microshop.supplier.infrastructure.inventory;

import com.microshop.supplier.application.InventoryReceiptRejectedException;
import com.microshop.supplier.application.InventoryUnavailableException;
import com.microshop.supplier.application.port.InventoryReceiptClient;
import jakarta.servlet.http.HttpServletRequest;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;
import org.springframework.web.client.RestClientException;
import org.springframework.web.client.RestClientResponseException;
import org.springframework.web.context.request.RequestContextHolder;
import org.springframework.web.context.request.ServletRequestAttributes;

import java.util.List;
import java.util.UUID;

@Component
class InventoryHttpReceiptClient implements InventoryReceiptClient {
    private static final String INTERNAL_API_KEY_HEADER = "X-MicroShop-Internal-Key";
    private static final String CORRELATION_ID_HEADER = "X-Correlation-ID";

    private final RestClient client;
    private final InventoryClientProperties properties;

    InventoryHttpReceiptClient(RestClient.Builder builder, InventoryClientProperties properties) {
        this.client = builder.baseUrl(properties.baseUrl()).build();
        this.properties = properties;
    }

    @Override
    public InventoryReceiptResponse receive(InventoryStockReceipt receipt) {
        try {
            var response = client.post()
                    .uri("/_internal/inventory/stock-receipts")
                    .contentType(MediaType.APPLICATION_JSON)
                    .header(INTERNAL_API_KEY_HEADER, properties.internalApiKey())
                    .header(CORRELATION_ID_HEADER, correlationId())
                    .body(new InventoryStockReceiptRequest(
                            receipt.receiptId(),
                            receipt.sourcePurchaseOrderId(),
                            receipt.items().stream()
                                    .map(item -> new InventoryStockReceiptItemRequest(item.productId(), item.quantity()))
                                    .toList()))
                    .retrieve()
                    .body(InventoryStockReceiptResponse.class);

            if (response == null || response.receiptId() == null) throw new InventoryUnavailableException();
            return new InventoryReceiptResponse(response.receiptId(), response.applied());
        } catch (RestClientResponseException exception) {
            if (exception.getStatusCode().is4xxClientError()) throw new InventoryReceiptRejectedException();
            throw new InventoryUnavailableException();
        } catch (RestClientException exception) {
            throw new InventoryUnavailableException();
        }
    }

    private static String correlationId() {
        var attributes = RequestContextHolder.getRequestAttributes();
        if (attributes instanceof ServletRequestAttributes servletAttributes) {
            HttpServletRequest request = servletAttributes.getRequest();
            var correlationId = request.getHeader(CORRELATION_ID_HEADER);
            if (correlationId != null && !correlationId.isBlank()) return correlationId;
        }

        return UUID.randomUUID().toString();
    }

    private record InventoryStockReceiptRequest(UUID receiptId, UUID sourcePurchaseOrderId, List<InventoryStockReceiptItemRequest> items) { }
    private record InventoryStockReceiptItemRequest(String productId, int quantity) { }
    private record InventoryStockReceiptResponse(UUID receiptId, boolean applied) { }
}
