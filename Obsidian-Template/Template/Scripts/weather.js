async function weather(tp) {
    let url = 'https://www.tianqi.com/binhuqu/';
    let weather = '无锡 天气获取失败';
    try {
        let res = await tp.web.request({url: url, method: "GET"});
        res = res.replace(/\s/g,'');
        let r = /<ddclass="weather">[\s\S]*?<\/dd>/g;
        let data = r.exec(res)[0];
        r = /<span><b>(.*?)<\/b>(.*?)<\/span>/g;
        data = r.exec(data);
        weather = '无锡 ' + data[1] + ' ' + data[2];
    } catch(e) {
        console.error(e);
    }
    return weather;
}
module.exports = weather;