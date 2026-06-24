import http from "k6/http";
import { check, sleep } from "k6";

export const options = {
  vus: 2,
  duration: "15s",
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<1000"],
  },
};

const gatewayBaseUrl = __ENV.GATEWAY_BASE_URL || "http://localhost:5027";

export default function () {
  const health = http.get(`${gatewayBaseUrl}/health`);
  check(health, {
    "gateway health is 200": (response) => response.status === 200,
  });

  const catalog = http.get(`${gatewayBaseUrl}/catalog/products`);
  check(catalog, {
    "catalog route is successful": (response) => response.status === 200,
    "correlation id is returned": (response) =>
      Boolean(response.headers["X-Correlation-ID"]),
  });

  sleep(1);
}
