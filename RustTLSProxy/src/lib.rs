use std::thread;
use std::ffi::CStr;
use std::os::raw::c_char;
use tiny_http::{Server, Response, Method};
use reqwest::blocking::Client;
use reqwest::header::{HeaderMap, HeaderName, HeaderValue};
use std::str::FromStr;

#[no_mangle]
pub extern "C" fn StartBridge(listen_port: i32, target_url_ptr: *const c_char) -> i32 {
    // 解析目标地址
    let c_str = unsafe {
        if target_url_ptr.is_null() { return -1; }
        CStr::from_ptr(target_url_ptr)
    };
    let target_base_url = match c_str.to_str() {
        Ok(s) => s.trim_end_matches('/'),
        Err(_) => return -2,
    };
    
    let target_url = target_base_url.to_string();

    // 开启新线程运行
    thread::spawn(move || {
        let addr = format!("127.0.0.1:{}", listen_port);
        let server = match Server::http(&addr) {
            Ok(s) => s,
            Err(e) => {
                eprintln!("Failed to start local server: {}", e);
                return;
            }
        };

        let client = Client::builder()
            .use_rustls_tls() // 用自带 TLS
            .danger_accept_invalid_certs(true)
            .build()
            .unwrap_or_default();

        println!("[RustBridge] Listening on {} -> {}", addr, target_url);

        for mut request in server.incoming_requests() {
            let full_url = format!("{}{}", target_url, request.url());
            
            // 转换 Method
            let method = match request.method() {
                Method::Get => reqwest::Method::GET,
                Method::Post => reqwest::Method::POST,
                Method::Put => reqwest::Method::PUT,
                Method::Delete => reqwest::Method::DELETE,
                _ => reqwest::Method::GET, 
            };

            // 读取 Body
            let mut body_vec = Vec::new();
            let _ = request.as_reader().read_to_end(&mut body_vec);

            let mut headers = HeaderMap::new();
            for h in request.headers() {
                let name_str = h.field.to_string();
                let val_str = h.value.to_string();

                if let Ok(name) = HeaderName::from_str(&name_str) {
                    if let Ok(val) = HeaderValue::from_str(&val_str) {
                        if name != reqwest::header::HOST {
                            headers.insert(name, val);
                        }
                    }
                }
            }

            // 发送请求
            let resp_result = client.request(method, &full_url)
                .headers(headers)
                .body(body_vec)
                .send();

            match resp_result {
                Ok(resp) => {
                    let status_code = resp.status().as_u16();
                    
                    if let Ok(bytes) = resp.bytes() {
                        let response = Response::from_data(bytes.to_vec())
                            .with_status_code(status_code);
                        let _ = request.respond(response);
                    }
                },
                Err(e) => {
                    let _ = request.respond(Response::from_string(format!("Bridge Error: {}", e)).with_status_code(502));
                }
            }
        }
    });

    return 0;
}