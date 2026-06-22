// Sets a new cookie with one year of expiration time
function setCookie(cname, cvalue) {
    var d = new Date();
    d.setTime(d.getTime() + (365 * 24 * 60 * 60 * 1000));
    var expires = "expires=" + d.toUTCString();
    document.cookie = cname + "=" + cvalue + ";" + expires + "; SameSite=Strict;";
}

// Retrieves a cookie
function getCookie(cname) {
    var name = cname + "=";
    var decodedCookie = decodeURIComponent(document.cookie);
    var ca = decodedCookie.split(';');
    for (var i = 0; i < ca.length; i++) {
        var c = ca[i];
        while (c.charAt(0) == ' ') {
            c = c.substring(1);
        }
        if (c.indexOf(name) == 0) {
            return c.substring(name.length, c.length);
        }
    }
    return "";
}

function upperCase(val) {
    return String(val).charAt(0).toUpperCase() + String(val).slice(1);
}

// Declares variables
var currenttab = "alle"
var feedsArr = []
var newsArr;
var loadcontent = false;
var jumptotop = false;

// Checks for cookie
feedsCookie = getCookie('feeds');
if (feedsCookie == '') {
    feedsArr = ['ard', 'inland', 'ausland']
}
else {
    feedsArr = feedsCookie.split('|')
}
contentCookie = getCookie('loadcontent');
if (contentCookie == 'false') {
    loadcontent = false;
}
else {
    loadcontent = true;
}

var datafile;

// Fetches news data and updates page once all sources have been fetched
function getFeed(RSS_URL, SRC_NAME, ressort) {

    $('#content').html('<img src="images/preloading.gif" id="preloader" />');

    $.ajax({
        url: RSS_URL,
        type: 'GET',
        dataType: 'xml',
        success(response) {
            datafile = response;
            console.log('Fetched ' + SRC_NAME)
            data = response.documentElement;
            const items = data.querySelectorAll("item");
            let html = ``;
            // Add all articles to news Array
            for (var i = 0; i < items.length; i++) {
                var el = items[i];
                var json = $.xml2json(el).item;

                if (json['pubDate']) {
                    var pubDate = json['pubDate'];
                    pubDate = Date.parse(pubDate);
                }
                else if (json['dc:date']) {
                    var pubDate = json['dc:date'];
                    pubDate = Date.parse(pubDate);
                }
                else {
                    var pubDate = '';
                }
                var title = json['title'];
                var link = json['link'];
                var description = json['description'];
                var source = SRC_NAME;
                if (json['content:encoded']) {
                    var content = json['content:encoded'];
                }
                else {
                    var content = '';
                }
                if (loadcontent == true && content != '') {
                    var articleArr = [pubDate, title, link, content, source];
                }
                else {
                    var articleArr = [pubDate, title, link, description, source];
                }
                newsArr.push(articleArr)
            }
            // Sort news by date descending
            newsArr.sort((a, b) => b[0] - a[0]);
            newsDone++
            console.log(SRC_NAME + ' fetched')
            if (newsDone == newsNeeded) {
                html += '<div id="feedtitle">' + SRC_NAME + '</div>';
                // Create html for news
                for (var t = 0; t < newsArr.length; t++) {
                    var ArticleDate = new Date(Number(newsArr[t][0]))
                    var d = ArticleDate.getDate(); d = ("0" + d).slice(-2);
                    var m = ArticleDate.getMonth() + 1; m = ("0" + m).slice(-2);
                    var y = ArticleDate.getFullYear()
                    var H = ArticleDate.getHours(); H = ("0" + H).slice(-2);
                    var M = ArticleDate.getMinutes(); M = ("0" + M).slice(-2);
                    var FormattedDate = d + '.' + m + '.' + y + ' ' + H + ':' + M
                    description = newsArr[t][3]
                    description = description.replace("]]>", "")
                    html += `
                  <article>
                    <h3>
                      <a href="${newsArr[t][2]}" target="_blank" rel="noopener">
                        ${newsArr[t][1]}
                      </a>
                    </h3>
                    <p>
                      ${description}
                    </p>
                    ${newsArr[t][4]}, ${FormattedDate}
                  </article>
                `;
                }
                // Output news in html format
                //contentDIV = document.getElementById('content')
                if (currenttab == ressort) {
                    $('#content').html(html)
                    if (jumptotop) { scroll(0, 0) }
                    console.log('News page updated');
                }
                else {
                    console.log('Switched to other tab, news update skipped.')
                }

                //newsbuttonDIV = document.getElementById('control-news')
                document.getElementById('control-' + ressort).style.backgroundImage = "url(images/" + ressort + ".png)";
                newsbuttonDIV.style.backgroundColor = "Highlight";
            }
        },
        error(jqXHR, status, errorThrown) {
            console.log('failed to fetch ' + SRC_NAME)
        },

    });
}

// Triggers the fetch of news data
function updateNews(ressort) {
    var feedTitle = upperCase(ressort);
    var feedURL = '';
    newsbuttonDIV = document.getElementById('control-'+ressort)
    newsbuttonDIV.style.backgroundImage = "url(images/loading.gif)";
    contentDIV = document.getElementById('content')
    newsArr = []
    newsDone = 0
    newsNeeded = 1 //required feeds to update page
    console.log('update requested for ' + ressort);
    switch (ressort) {
        case 'alle':
            feedURL = "https://www.tagesschau.de/infoservices/alle-meldungen-100~rss2.xml";
            break;
        case 'inland':
            feedURL = "https://www.tagesschau.de/inland/index~rss2.xml";
            break;
        case 'ausland':
            feedURL = "https://www.tagesschau.de/ausland/index~rss2.xml";
            break;
        case 'wirtschaft':
            feedURL = "https://www.tagesschau.de/wirtschaft/index~rss2.xml";
            break;
        case 'wissen':
            feedURL = "https://www.tagesschau.de/wissen/index~rss2.xml";
            break;
        default:
            console.log('unknown news ressort')
    }
    getFeed(feedURL, feedTitle, ressort)
    scroll(0, 0)
}

// convert Unix timestamp to human readable date
function timestamp2date(ts) {
    var ts_ms = ts * 1000;
    var date_ob = new Date(ts_ms);
    var year = date_ob.getFullYear();
    var month = ("0" + (date_ob.getMonth() + 1)).slice(-2);
    var date = ("0" + date_ob.getDate()).slice(-2);

    var hours = ("0" + date_ob.getHours()).slice(-2);
    var minutes = ("0" + date_ob.getMinutes()).slice(-2);
    var seconds = ("0" + date_ob.getSeconds()).slice(-2);

    // date as YYYY-MM-DD format
    //return(year + "-" + month + "-" + date);
    return (date);
}

function timestamp2fulldate(ts) {
    var ts_ms = ts * 1000;
    var date_ob = new Date(ts_ms);
    var year = date_ob.getFullYear();
    var month = ("0" + (date_ob.getMonth() + 1)).slice(-2);
    var date = ("0" + date_ob.getDate()).slice(-2);

    var hours = ("0" + date_ob.getHours()).slice(-2);
    var minutes = ("0" + date_ob.getMinutes()).slice(-2);
    var seconds = ("0" + date_ob.getSeconds()).slice(-2);

    // date as YYYY-MM-DD format
    return (year + "-" + month + "-" + date + ' ' + hours + ':' + minutes);
}

// Switches to news or triggers a news update
function clickNews(ressort) {
    jumptotop = false
    if (currenttab != ressort) { jumptotop = true }
    currenttab = ressort
    $('.controlbutton').css('background-color', 'transparent');
    $('#control-' + ressort).css('background-color', 'Highlight');
    console.log('clicked to update ' + ressort);
    updateNews(ressort);
}

// Displays the settings page
function clickSettings() {
    try {
        appVersion = Windows.ApplicationModel.Package.current.id.version;
        appstring = `${appVersion.major}.${appVersion.minor}.${appVersion.build}`;
    }
    catch (e) {
        appstring = '';
    }

    currenttab = "settings"
    $('.controlbutton').css('background-color', 'transparent');
    $('#control-settings').css('background-color', 'Highlight');
    contentDIV = document.getElementById('content')
    settingsout = '<div style="font-size:16px; sans-serif; margin-left:10%;width:80%;">'
        + '<div id="feedtitle" style="margin-bottom:20px;">Settings</div>'
        + '<div id="version" style="text-align:left;width:80%;margin-left:10%;margin-bottom:15px;">DayView by mavodev v' + appstring + '</div>'
        + '<div style="text-align:left;width:80%;margin-left:10%;margin-bottom:10px;">Content settings:</div>'
        + '<div style="width:80%;margin-left:10%;">'
        + '<label class="container">Include pictures'
    if (loadcontent == true) {
        settingsout += '  <input type="checkbox" id="content_loadall" checked="checked">'
    }
    else {
        settingsout += '  <input type="checkbox" id="content_loadall">';
    }
    settingsout += '  <span class="checkmark"></span>'
        + '</label>'

    settingsout += '<div id="settings-apply-button" class="settings-apply" onclick="applySettings()">Apply</div>';

    contentDIV.innerHTML = settingsout;
    console.log('Settings page updated');
}

// Applies settings and saves them as a cookie
function applySettings() {
    applyButtonDIV = document.getElementById('settings-apply-button')
    applyButtonDIV.style.backgroundColor = '#a0a0a0';

    if (document.getElementById("content_loadall").checked) {
        setCookie('loadcontent', 'true');
        loadcontent = true;
    }
    else {
        setCookie('loadcontent', 'false');
        loadcontent = false;
    }

    clickNews('alle');
}

// Hides the control bar
function clickHidecontrol() {
    // Toggle news button
    newsDIV = document.getElementById('control-news')
    if (newsDIV.style.display === "none") {
        newsDIV.style.display = "inline-block";
    } else {
        newsDIV.style.display = "none";
    }
    // Toggle stats button
    statsDIV = document.getElementById('control-stats')
    if (statsDIV.style.display === "none") {
        statsDIV.style.display = "inline-block";
    } else {
        statsDIV.style.display = "none";
    }
    // Toggle settings button
    statsDIV = document.getElementById('control-settings')
    if (statsDIV.style.display === "none") {
        statsDIV.style.display = "inline-block";
    } else {
        statsDIV.style.display = "none";
    }
    // Toggle control bar
    controlDIV = document.getElementById('controls')
    controlbuttonDIV = document.getElementById('control-hide')
    if (controlDIV.style.width === "55px") {
        controlDIV.style.width = "100%";
        controlDIV.style.backgroundColor = "#111111";
        controlbuttonDIV.style.backgroundImage = "url(images/arrow-right.png)";
    } else {
        controlDIV.style.width = "55px";
        controlDIV.style.backgroundColor = "";
        controlbuttonDIV.style.backgroundImage = "url(images/arrow-left.png)";
    }
}

$(document).ready(function () {
    document.onselectstart = new Function("return false")
});

updateNews('alle')
setInterval(updateNews(currenttab), 1800000)