/**
 * 获取日记元数据，包括位置、天气和农历日期
 * 需要配置高德地图 API 和聚合数据 API 的密钥
 */
module.exports = async (tp, config = {}) => {
    // 高德地图 API 配置
    const gaodeApiKey = "5bababc2779f92d967908b900c158e4e"; // ⚠️请替换为您的高德地图 API 密钥
    // 聚合数据 API 配置（用于农历日期）
    const juheApiKey = "a63532bd65f446cd3aa5118d5770926a"; // ⚠️请替换为您的聚合数据 API 密钥

    // 检查是否已配置 Key
    if (gaodeApiKey === "YOUR_AMAP_API_KEY") {
        return {
            location: "请配置高德API Key",
            weather: "请配置高德API Key",
            lunarDate: juheApiKey === "YOUR_JUHE_API_KEY" ? "请配置聚合API Key" : "无法获取(高德Key未配置)"
        };
    }

    // 从配置中解构可能的值
    let { location: inputLocation = "", adcode: inputAdcode = "", weather: inputWeather = "" } = config;

    // 初始化元数据变量
    let location = inputLocation;
    let adcode = inputAdcode;
    let weather = inputWeather;
    let lunarDate = "无法获取农历日期";

    // 辅助函数：优先使用 Obsidian 的 requestUrl (解决 CORS/Mixed Content)，降级使用 fetch
    async function fetchJson(url) {
        try {
            // 尝试使用 Obsidian 的 requestUrl API (绕过 CORS/Mixed Content)
            if (typeof app !== 'undefined' && app.requestUrl) {
                const response = await app.requestUrl({ url: url });
                if (response.status !== 200) {
                     throw new Error(`HTTP error! status: ${response.status}`);
                }
                return response.json;
            }
            
            // 降级使用 fetch
            const response = await fetch(url);
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return await response.json();
        } catch (e) {
            console.error(`Fetch error for ${url}:`, e);
            throw e;
        }
    }

    // 1. 获取位置 (IP定位)
    if (!location) {
        try {
            const geoURL = `https://restapi.amap.com/v3/ip?key=${gaodeApiKey}&output=json`;
            const geoData = await fetchJson(geoURL);
            
            if (geoData.status === "1") {
                location = `${geoData.province}${geoData.city}`; 
                adcode = geoData.adcode; 
            } else {
                location = `定位失败: ${geoData.info || '未知错误'}`;
                console.warn(`高德IP定位失败: ${geoData.info}`);
            }
        } catch (error) {
            console.error("IP定位请求异常:", error);
            location = "定位请求超时/异常";
        }
    }

    // 2. 获取 Adcode (如果只有位置没有adcode)
    if (!adcode && location && !location.includes("失败") && !location.includes("请求")) {
        try {
            const geocodeURL = `https://restapi.amap.com/v3/geocode/geo?key=${gaodeApiKey}&address=${encodeURIComponent(location)}`;
            const geocodeData = await fetchJson(geocodeURL);

            if (geocodeData.status === "1" && geocodeData.geocodes?.length > 0) {
                adcode = geocodeData.geocodes[0].adcode;
            } else {
                console.warn(`地理编码失败: ${geocodeData.info}`);
            }
        } catch (error) {
            console.error("地理编码请求异常:", error);
        }
    }

    // 3. 获取天气
    if (adcode) {
        if (!weather) {
            try {
                const weatherURL = `https://restapi.amap.com/v3/weather/weatherInfo?key=${gaodeApiKey}&city=${adcode}&extensions=all&output=json`;
                const weatherData = await fetchJson(weatherURL);

                if (weatherData.status === "1" && weatherData.forecasts?.length > 0) {
                    const forecast = weatherData.forecasts[0];
                    const { dayweather, nightweather, daytemp, nighttemp } = forecast.casts[0];
                    weather = `${dayweather}/${nightweather}, ${nighttemp}~${daytemp}℃`;
                } else {
                    weather = `天气获取失败: ${weatherData.info}`;
                }
            } catch (error) {
                console.error("天气请求异常:", error);
                weather = "天气服务异常";
            }
        }
    } else {
        if (!weather) weather = "无位置信息无法获取天气";
    }

    // 4. 获取农历 (聚合数据)
    if (juheApiKey === "YOUR_JUHE_API_KEY") {
        lunarDate = "请配置聚合API Key";
    } else {
        try {
            let apiDate = tp.date.now("YYYY-MM-DD");
            // 尝试使用 http，配合 requestUrl 可解决 Mixed Content 问题
            // 增加 user-agent 头部，因为某些 API 会拦截无头部的请求
            // 聚合数据可能对 requestUrl 的特定请求头敏感，尝试使用 fetch 并加上 no-cors (虽然拿不到结果，但排除跨域)
            // 实际上 requestUrl 是 Node 环境，不需要 no-cors
            
            // 聚合数据有时候对 HTTP/HTTPS 有要求，或者对 Referer 有要求。
            // 这里我们尝试把请求参数 urlencode 一下，虽然 date 和 key 通常不需要
            const lunarURL = `http://v.juhe.cn/calendar/day?date=${apiDate}&key=${juheApiKey}`;
            
            // 单独为农历增加一个 try-catch 块，并尝试打印更多错误信息
            let JsonData;
            try {
                JsonData = await fetchJson(lunarURL);
            } catch (innerError) {
                console.error("农历API请求失败，尝试使用 fetch fallback:", innerError);
                // 如果 requestUrl 失败，尝试 fetch (虽然可能跨域)
                 const response = await fetch(lunarURL);
                 JsonData = await response.json();
            }

            if (JsonData.error_code === 0 && JsonData.result) {
                lunarDate = JsonData.result.data.lunar || "数据解析错误";
            } else {
                lunarDate = `农历失败: ${JsonData.reason || JsonData.error_code}`;
            }
        } catch (error) {
            console.error("农历请求异常:", error);
            lunarDate = `农历异常: ${error.message}`;
        }
    }

    return {
        location,
        weather,
        lunarDate
    };
};
