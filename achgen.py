import requests
import os
import sys
import shutil
from bs4 import BeautifulSoup

maindir = os.path.dirname(__file__)
if maindir != '':
    maindir = maindir.replace('\\','/')

class AchGen(object):
    def __init__(self, filename, lang=""):
        content = ""
        self.lang = lang
        self.session = requests.session()

        try:
            with open(filename, 'r', encoding='utf-8') as file:
                content = file.read()
        except:pass
        self.soup = BeautifulSoup(content, 'html.parser')
        self.appid = "0"
        if self.soup:
            _id = self.soup.find('div', {'class': 'scope-app'})
            if _id:
                self.appid = _id.get('data-appid')

    def getAchievements(self):
        achievements = []
        achievements_table = self.soup.find('div', {'id': 'js-achievements'})
        if not achievements_table:
            return achievements

        achievements_table = achievements_table.find('tbody')

        translation = self.LoadSteamDoc(self.lang);
        imgdir = '/'.join((maindir,self.appid,'images'))

        for row in achievements_table.find_all('tr'):
            tds = row.find_all('td')
            if len(tds) < 3:
                continue
            data = {}
            data["name"] = tds[0].text
            data["displayName"] = ""
            data["description"] = ""
            split = tds[1].text.splitlines()
            if len(split) >= 4:
                text = split[1]
                displayName = text = split[1]
                description = split[3].strip()
                
                if text in translation:
                    displayName = translation[text]['displayName']
                    description = translation[text]['description']

                data["displayName"] = displayName
                data["description"] = description
            
            data["hidden"] = "0"
            hidden = tds[1].find('svg', {'aria-hidden': 'true'}) != None
            if hidden:
                data["hidden"] = "1"

            img = tds[2].find_all('img')
            icon = img[0].get('data-name')
            icongray = img[1].get('data-name')
            data["icon"] = f'images/{icon}'
            data["icongray"] = f'images/{icongray}'
            
            if make_dir(imgdir):
                src = '/'.join((filesFolder,icon))
                dst = '/'.join((imgdir,icon))
                copy_file(src,dst)
                
                src = '/'.join((filesFolder,icongray))
                dst = '/'.join((imgdir,icongray))
                copy_file(src,dst)
            
            achievements.append(data)
        return achievements

    def getStats(self):
        stats = []
        stats_table = self.soup.find('div', {'id': 'js-stats'})
        if not stats_table:
            return stats
        
        stats_table = stats_table.find('tbody')

        for row in stats_table.find_all('tr'):
            tds = row.find_all('td')
            if len(tds) < 3:
                continue
            
            data = {}
            data["name"] = tds[0].text
            data["displayName"] = tds[1].text
            data["defaultValue"] = tds[2].text
            stats.append(data)
        return stats

    def getDLC(self):
        dlc = []
        dlc_table = self.soup.find('div', {'id': 'dlc'})
        if not dlc_table:
            return dlc
        
        dlc_table = dlc_table.find('tbody')

        for row in dlc_table.find_all('tr'):
            if row.get('hidden') is not None:
                continue
            
            tds = row.find_all('td')
            
            if len(tds) < 2:
                continue
            
            line = f'{tds[0].text}={tds[1].text}'
            dlc.append(line)
        return dlc

    def LoadSteamDoc(self,lang):
        translation = {}
        try:
            resp = self.session.get(f'https://steamcommunity.com/stats/{achgen.appid}/achievements/')
            orig = resp.text
            cookies = {'Steam_Language': lang}
            resp = self.session.get(f'https://steamcommunity.com/stats/{achgen.appid}/achievements/', cookies=cookies)
            trns = resp.text

            origsoup = BeautifulSoup(orig, 'html.parser')
            trnssoup = BeautifulSoup(trns, 'html.parser')
            
            orig = origsoup.find_all('div', {'class': 'achieveRow'})
            trns = trnssoup.find_all('div', {'class': 'achieveRow'})
            
            cnt = 0
            length = len(orig)
            
            while cnt < length:
                achieveTxt = orig[cnt].find('div', {'class': 'achieveTxt'})
                origDisplayName = achieveTxt.find('h3').text
                achieveTxt = trns[cnt].find('div', {'class': 'achieveTxt'})
                trnsDisplayName = achieveTxt.find('h3').text
                trnsDescription = achieveTxt.find('h5').text
                translation[origDisplayName] = {'displayName': trnsDisplayName, 'description': trnsDescription}
                cnt+=1
        except:pass
        return translation

def formatJson(content):
    s = '[\n'
    count = 0
    length = len(content)

    for line in content:
        s+= '  {\n'
        _count = 0
        _length = len(line)
        for key,value in line.items():
            s+= f'    "{key}": "{value}"'
            _count+=1
            if _count < _length:
                s+= ','
            s+= '\n'
        s+= '  }'
        count += 1
        if count < length:
            s+= ','
        s+= '\n'
    s+= ']'
    return s

def make_dir(path,forced=True):
    if os.path.isdir(path):
        return True
    if os.path.exists(path):
        if not forced:
            return False

        if not delete(path):
            return False
    try:
        os.makedirs(path, exist_ok=True)
    except:
        return False
    return True

def copy_file(src,dst):
    try:
        shutil.copy2(src, dst)
    except:
        return False
    return True

def saveFile(name,content,mode='stats'):
    if not content:
        return
    if len(content) == 0:
        return
    
    filename = '/'.join((folder,name))
    
    with open(filename, 'w', encoding='utf-8') as file:
        if mode == 'stats':
            file.write(formatJson(content))
        elif isinstance(content, list):
            file.write('\n'.join(map(str, content)))
        else:
            file.write(str(content))

usage = ["Achievements Generator",
        "Version 1.0",
        "Programmed by Colmines92",
        "",
        "USAGE:",
        "\t1. Search your game at https://steamdb.info/",
        "\t2. Choose your game id from the list.",
        "\t3. Click on achievements, then download the page.",
        "\t4. Drag the downloaded html into the app executable icon and it will generate achievements.json",
        ""
        "\t   Specify (or not) the desired language(only if online) and wait for it to finish."]
usage = '\n'.join(map(str, usage))

def printUsage():
    print(usage)
    input("")
    exit()

if __name__ == "__main__":
    if len(sys.argv) < 2:
        printUsage()
    
    filename = sys.argv[1].replace('\\', '/')
    lang = ""
    if len(sys.argv) > 2:
        lang = sys.argv[2].lower();
    else:
        lang = input("Specify desired language (Default: english)\n")

        if (lang == "english"):
            lang = ""

    if not os.path.isfile(filename):
        printUsage()
    
    global filesFolder
    filesFolder = os.path.splitext(filename)[0] + '_files'
    
    global achgen
    achgen = AchGen(filename,lang=lang)
    
    global folder
    folder = '/'.join((maindir,achgen.appid))
    
    achievements = achgen.getAchievements()
    stats = achgen.getStats()
    dlc = achgen.getDLC()

    if not make_dir(folder):
        exit()
    
    saveFile('steam_appid.txt', achgen.appid, 'text')
    
    if achievements:
        saveFile('achievements.json', achievements)
    if stats:
        saveFile('stats.json', stats)
    if dlc:
        saveFile('DLC.txt', dlc, mode='dlc')