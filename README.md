# BARY OCR - Hệ Thống Số Hóa Và Trích Xuất Dữ Liệu Tự Động

BARY OCR là một giải pháp toàn diện giúp doanh nghiệp tự động hóa quá trình số hóa tài liệu. Hệ thống sử dụng công nghệ Nhận dạng ký tự quang học (OCR) kết hợp với Trí tuệ nhân tạo (LLM - Mô hình ngôn ngữ lớn) để không chỉ đọc văn bản từ hình ảnh/PDF mà còn thông minh phân loại tài liệu và bóc tách các trường thông tin cần thiết một cách hoàn toàn tự động.

## 🌟 Tính Năng Nổi Bật

- **Quản lý theo Lô (Batch):** Hỗ trợ tải lên và xử lý hàng loạt tài liệu theo từng dự án hoặc lô hồ sơ.
- **Xử lý OCR Chính Xác:** Tự động chuyển đổi file PDF, hình ảnh thành văn bản text sạch. Hệ thống có cơ chế xử lý nền để tránh tắc nghẽn.
- **Phân Loại Tài Liệu Bằng AI (Classification):** Ứng dụng LLM (chạy local qua llama.cpp) để tự động nhận diện và gắn nhãn tài liệu (ví dụ: Sơ đồ một sợi, Thiết bị, Hợp đồng, v.v.) dựa trên nội dung OCR.
- **Bóc Tách Dữ Liệu Chuyên Sâu (Extraction):** Dựa trên nhãn đã phân loại, AI tiếp tục trích xuất các trường dữ liệu (Metadata) dưới định dạng JSON có cấu trúc một cách chính xác.
- **Review và Xác Nhận:** Giao diện trực quan cho phép người dùng đối chiếu lại kết quả OCR và dữ liệu bóc tách so với bản gốc PDF.
- **Bảo mật và Tốc độ:** Các mô hình AI (Gemma 2, Qwen 2.5) được chạy hoàn toàn cục bộ (Local LLM), đảm bảo dữ liệu không bị rò rỉ ra bên ngoài.

## 🏗️ Kiến Trúc Hệ Thống

Hệ thống được thiết kế theo mô hình Microservices và điều phối bởi **.NET Aspire**.

### Công nghệ sử dụng:
1. **Backend API & Worker (.NET 10 - C#):** 
   - Xây dựng theo chuẩn Clean Architecture (Core, Infrastructure, Api, Worker).
   - Database: **PostgreSQL** (thông qua Entity Framework Core).
   - Message Broker: **RabbitMQ** (quản lý hàng đợi cho các tác vụ OCR và AI).
   - Caching: **Redis**.
   - Storage: **MinIO** (lưu trữ file PDF, hình ảnh, file markdown text sạch).
2. **Frontend (Nuxt.js / Vue.js):**
   - Giao diện người dùng hiện đại, hiển thị PDF Viewer tích hợp.
3. **OCR Worker (Python):**
   - Đảm nhiệm việc parse file PDF, render ảnh và chạy thư viện OCR chuyên dụng.
4. **LLM Server (llama.cpp):**
   - Server AI độc lập tối ưu cho GPU (chạy trong Docker container) đóng vai trò nhận diện và trích xuất dữ liệu.

## 🚀 Hướng Dẫn Cài Đặt Và Vận Hành

### Yêu Cầu Hệ Thống
- Docker và Docker Compose.
- .NET 10 SDK.
- Node.js (cho Frontend).
- GPU (Nvidia) với dung lượng VRAM tối thiểu 16GB (để chạy LLM mượt mà với Context Size lớn).

### 1. Chuẩn bị Thư mục Models và Tải Models
Trước khi khởi chạy hệ thống, bạn cần chuẩn bị thư mục chứa các file mô hình Trí tuệ nhân tạo (định dạng `.gguf`). `llm_server` trong Docker sẽ tự động mount thư mục này để nạp model.

1. Tạo thư mục `models` ở thư mục gốc của dự án (nếu chưa có):
```bash
mkdir models
```

2. Tải các mô hình cần thiết từ Hugging Face:
Dưới đây là danh sách **CHÍNH XÁC** các mô hình cần thiết cho hệ thống BARY OCR. Vui lòng tải đầy đủ và đúng tên file.

**A. Mô hình Phân loại & Bóc tách (LLM Text & Vision)**
- **Link Hugging Face tham khảo:** [google/gemma-4-E2B-it-GGUF](https://huggingface.co/models?search=gemma-4-E2B_q4_0-it) (hoặc repo lưu trữ nội bộ)
- **Các file bắt buộc tải:**
  - `gemma-4-E2B_q4_0-it.gguf` (Model ngôn ngữ chính)
  - `gemma-4-E2B-it-mmproj.gguf` (Vision Projector cho model ngôn ngữ nếu dùng đa phương thức)

**B. Mô hình OCR Vision (PaddleOCR-VL)**
- **Link Hugging Face tham khảo:** [PaddlePaddle/PaddleOCR-VL-1.6-GGUF](https://huggingface.co/models?search=PaddleOCR-VL-1.6-GGUF) (hoặc repo lưu trữ nội bộ)
- **Các file bắt buộc tải:**
  - `PaddleOCR-VL-1.6-GGUF.gguf` (Model OCR chính)
  - `PaddleOCR-VL-1.6-GGUF-mmproj.gguf` (Vision Projector tiêu chuẩn)
  - `PaddleOCR-VL-1.6-GGUF-mmproj-spotting.gguf` (Vision Projector chuyên dụng cho Text Spotting)

Ví dụ lệnh tải model bằng `wget` (thay URL bằng link download thực tế trên Hugging Face của bạn):
```bash
wget -O models/gemma-4-E2B_q4_0-it.gguf "URL_DOWNLOAD_GEMMA_Q4"
wget -O models/gemma-4-E2B-it-mmproj.gguf "URL_DOWNLOAD_GEMMA_MMPROJ"
wget -O models/PaddleOCR-VL-1.6-GGUF.gguf "URL_DOWNLOAD_PADDLE_OCR"
wget -O models/PaddleOCR-VL-1.6-GGUF-mmproj.gguf "URL_DOWNLOAD_PADDLE_MMPROJ"
wget -O models/PaddleOCR-VL-1.6-GGUF-mmproj-spotting.gguf "URL_DOWNLOAD_PADDLE_SPOTTING"
```

3. (Tùy chọn) Nếu mô hình của bạn có tên khác, hãy nhớ mở file `docker-compose.yml` hoặc `llm_server/entrypoint.sh` để cập nhật lại biến môi trường `MODEL_PATH` cho khớp.

### 2. (Tùy chọn) Tự build image llama.cpp từ mã nguồn GitHub
Nếu bạn không muốn tải image có sẵn hoặc muốn tự build để tối ưu cho dòng card đồ họa đặc thù của mình, bạn có thể clone và build `llama.cpp` từ mã nguồn gốc:

```bash
# 1. Clone repository chính thức của llama.cpp
git clone https://github.com/ggerganov/llama.cpp.git
cd llama.cpp

# 2. Build Docker image hỗ trợ CUDA (Nvidia GPU)
# Tên image ghcr.io/ggml-org/llama.cpp:server-cuda được dùng để file Dockerfile của hệ thống nhận diện
docker build -t ghcr.io/ggml-org/llama.cpp:server-cuda -f .devops/main-cuda.Dockerfile .

# 3. Trở lại thư mục gốc của dự án BARY OCR
cd ..
```

### 3. Chạy các dịch vụ nền tảng và LLM Server
Đảm bảo bạn đã tải model GGUF (ví dụ: `gemma-4-E2B_q4_0-it.gguf`) vào thư mục `models/` như bước 1.
Khởi động cơ sở dữ liệu, các dịch vụ đi kèm và cả LLM Server thông qua Docker Compose:
```bash
docker-compose up -d --build
```
*(Các dịch vụ bao gồm: PostgreSQL, Redis, RabbitMQ, MinIO và LLM Server)*

*Lưu ý: LLM Server đã được cấu hình sẵn trong docker-compose.yml tự động map thư mục `models/`, sử dụng GPU Nvidia, và thiết lập `CTX_SIZE=32768` để đảm bảo AI có đủ không gian nhớ phân tích các tài liệu dài.*

### 3. Chạy Backend (bằng .NET Aspire)
Mở solution bằng Visual Studio hoặc chạy qua CLI tại thư mục `backend/src/AutoOcrs.AppHost`:
```bash
dotnet run
```
Aspire sẽ tự động điều phối khởi chạy API, Worker và Frontend.

### 4. Chạy Frontend độc lập (Tùy chọn)
Nếu bạn muốn dev Frontend riêng, di chuyển vào thư mục `frontend`:
```bash
npm install
npm run dev
```

## 📝 Mẹo Cấu Hình LLM Prompt
Để hệ thống phân loại chính xác nhất, trong phần quản lý Nhãn (Labels), hãy viết hướng dẫn rõ ràng. 
Ví dụ:
- **Tốt:** *"Chọn nhãn này nếu tài liệu là bản vẽ kỹ thuật, có tiêu đề ghi rõ 'SƠ ĐỒ MỘT SỢI'..."*
- **Không tốt:** *"Đây là sơ đồ một sợi."* (Quá chung chung, AI dễ nhầm lẫn).
