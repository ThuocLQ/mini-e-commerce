# MicroShop Quiz Netlify

Web quiz static để deploy lên Netlify.

## Cấu trúc

```text
index.html
_headers
data/
  quiz-manifest.json
  quizzes/
    checkpoint-4.json
```

## Chạy local

Không mở bằng double-click nếu muốn auto load JSON bằng `fetch`.

Dùng một trong các lệnh:

```bash
python -m http.server 3000
```

hoặc:

```bash
npx serve .
```

Sau đó mở:

```text
http://localhost:3000
```

## Deploy Netlify

Cách nhanh nhất:

1. Kéo thả toàn bộ thư mục này vào Netlify Deploys.
2. Hoặc push thư mục này lên GitHub.
3. Netlify chọn repo đó và deploy.

## Thêm quiz mới

Thêm file:

```text
data/quizzes/lesson-19.json
```

Sau đó khai báo trong:

```text
data/quiz-manifest.json
```

Ví dụ:

```json
{
  "id": "lesson-19",
  "title": "Buổi 19 - RabbitMQ Intro",
  "description": "Quiz bài 19",
  "file": "./data/quizzes/lesson-19.json"
}
```

Rồi:

```bash
git add .
git commit -m "add lesson 19 quiz"
git push
```

Netlify sẽ auto deploy nếu đã nối GitHub.
