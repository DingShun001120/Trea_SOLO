/**
 * 获取日记元数据，包括位置、天气和农历日期
 * 需要配置高德地图 API 和聚合数据 API 的密钥
 */
module.exports = async (tp, config = {}) => {
    // 高德地图 API 配置
    const gaodeApiKey = "YOUR_AMAP_API_KEY"; // 替换为您的高德地图 API 密钥

    // 聚合数据 API 配置（用于农历日期）
    const juheApiKey = "YOUR_JUHE_API_KEY"; // 替换为您的聚合数据 API 密钥

    // 从配置中解构可能的值
    let { location: inputLocation = "", adcode: inputAdcode = "", weather: inputWeather = "" } = config;

    // 初始化元数据变量
    let location = inputLocation;
    let adcode = inputAdcode;
    let weather = inputWeather;
    let lunarDate = "无法获取农历日期";

    // 获取 IP 地址地理位置，除非已经有 location 值
    if (!location) {
        try {
            const geoURL = `https://restapi.amap.com/v3/ip?key=${gaodeApiKey}&output=json`;
            const geoResponseText = await tp.web.request(geoURL);
            const geoData = JSON.parse(geoResponseText);
            console.log("Geolocation API Response:", geoData); // 调试日志

            if (geoData.status === "1") {
                location = `${geoData.province}${geoData.city}`;  // 输出结果为：浙江省温州市
                adcode = geoData.adcode; // 使用 adcode 替代 citycode
            } else {
                location = "无法获取位置";
                console.log(`位置获取失败：${geoData.info}`);
            }
        } catch (error) {
            console.error("位置获取失败：", error);
            location = "无法获取位置";
        }
    } else {
        console.log("已提供位置，跳过IP地理位置获取。");
    }

    // 如果 adcode 没有值，但有 location，则通过地理编码 API 获取 adcode
    if (!adcode && location && location !== "无法获取位置") {
        try {
            const geocodeURL = `https://restapi.amap.com/v3/geocode/geo?key=${gaodeApiKey}&address=${encodeURIComponent(location)}`;
            const geocodeResponseText = await tp.web.request(geocodeURL);
            const geocodeData = JSON.parse(geocodeResponseText);
            console.log("Geocode API Response:", geocodeData); // 调试日志

            if (geocodeData.status === "1" && geocodeData.geocodes && geocodeData.geocodes.length > 0) {
                adcode = geocodeData.geocodes[0].adcode;
                console.log(`通过 location 获取到的 adcode: ${adcode}`);
            } else {
                console.log(`通过 location 获取 adcode 失败：${geocodeData.info}`);
            }
        } catch (error) {
            console.error("通过 location 获取 adcode 失败：", error);
        }
    }

    // 获取天气情况（使用 高德地图天气 API），除非已经有 weather 值
    if (adcode) {
        if (!weather) { // 仅当 weather 为空时才获取天气
            try {
                const weatherURL = `https://restapi.amap.com/v3/weather/weatherInfo?key=${gaodeApiKey}&city=${adcode}&extensions=all&output=json`;
                const weatherResponseText = await tp.web.request(weatherURL);
                const weatherData = JSON.parse(weatherResponseText);
                console.log("Weather API Response:", weatherData); // 调试日志

                if (weatherData.status === "1" && weatherData.forecasts && weatherData.forecasts.length > 0) {
                    const forecast = weatherData.forecasts[0];
                    const { dayweather, nightweather, daytemp, nighttemp } = forecast.casts[0];

                    weather = `${dayweather}/${nightweather}, ${nighttemp}~${daytemp}℃`;
                } else {
                    weather = "无法获取天气信息";
                    console.log(`天气获取失败：${weatherData.info}`);
                }
            } catch (error) {
                console.error("天气获取失败：", error);
                weather = "获取失败";
            }
        } else {
            console.log("已提供天气信息，跳过天气API调用。");
        }
    } else {
        console.log("未提供 adcode，跳过天气获取。");
    }

    // 获取农历日期（使用聚合数据 API）
    try {
        let apiDate = tp.date.now("YYYY-MM-DD");
        const lunarURL = `http://v.juhe.cn/calendar/day?date=${apiDate}&key=${juheApiKey}`;
        const resText = await tp.web.request(lunarURL);
        let JsonData = JSON.parse(resText);
        let lunarData = JsonData.result; // 根据API文档调整层级

        console.log("Lunar API Response:", lunarData); // 调试日志

        if (JsonData.error_code === 0 && lunarData) {
            lunarDate = lunarData.data.lunar || "无法获取农历日期"; // 根据API文档调整字段
        } else {
            lunarDate = "获取失败";
            console.log(`农历日期获取失败：${lunarData ? lunarData.reason : '未知原因'}`);
        }
    } catch (error) {
        console.error("农历日期获取失败：", error);
        lunarDate = "获取失败";
    }

    return {
        location,
        weather,
        lunarDate
    };
};
